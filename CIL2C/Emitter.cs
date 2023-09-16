using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CCG;
using CCG.Expressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CIL2C;

public static class Emitter
{
    public static CType EmitType(
        ref CBuilder builder,
        TypeDef type,
        out TypeSig signature)
    {
        signature = type.ToTypeSig();

        var name = type.FullName;
        var safeName = Utils.GetSafeName(name);

        if (type is { IsClass: true, IsEnum: false })
        {
            var packStruct = false;

            foreach (var attribute in type.CustomAttributes)
            {
                switch (attribute.TypeFullName)
                {
                    case "System.Runtime.InteropServices.StructLayoutAttribute":
                    {
                        var layoutKind = (LayoutKind)attribute.GetProperty("Value").Value;
                        if (layoutKind == LayoutKind.Explicit)
                        {
                            // TODO
                            throw new NotImplementedException("Explicit layouts in structs aren't supported!");
                        }

                        var pack = Convert.ToInt32(attribute.GetField("Pack").Value);
                        packStruct = pack == 1;
                        break;
                    }
                }
            }

            var structFields = new List<CStructField>();

            // TODO
            /*if (!type.IsValueType)
            {
                structFields.Add(new CStructField(Utils.Object, "header"));
            }*/

            switch (name)
            {
                case "System.Char": structFields.Add(new CStructField(false, CType.UInt16, "value")); break;
                case "System.Boolean": structFields.Add(new CStructField(false, CType.Boolean, "value")); break;
                case "System.SByte": structFields.Add(new CStructField(false, CType.Int8, "value")); break;
                case "System.Int16": structFields.Add(new CStructField(false, CType.Int16, "value")); break;
                case "System.Int32": structFields.Add(new CStructField(false, CType.Int32, "value")); break;
                case "System.Int64": structFields.Add(new CStructField(false, CType.Int64, "value")); break;
                case "System.Byte": structFields.Add(new CStructField(false, CType.UInt8, "value")); break;
                case "System.UInt16": structFields.Add(new CStructField(false, CType.UInt16, "value")); break;
                case "System.UInt32": structFields.Add(new CStructField(false, CType.UInt32, "value")); break;
                case "System.UInt64": structFields.Add(new CStructField(false, CType.UInt64, "value")); break;
                case "System.IntPtr": structFields.Add(new CStructField(false, CType.IntPtr, "value")); break;
                case "System.UIntPtr": structFields.Add(new CStructField(false, CType.UIntPtr, "value")); break;
            }

            /*foreach (var field in type.Fields)
            {
                if (field.IsStatic) continue;

                var cType = GetCType(ref types, field.FieldType);
                structFields.Add(new CStructField(false, cType, field.Name));
            }*/

            var fields = structFields.ToArray();
            builder.AddStruct(safeName, packStruct, fields);
            return new CType(safeName, true, structFields: fields);
        }

        if (type.IsEnum)
        {
            var enumFields = new List<CEnumField>();

            foreach (var field in type.Fields)
            {
                if (field.FieldType.FullName != signature.FullName) continue;

                var fieldSafeName = Utils.GetSafeName(field.FullName);
                var fieldValue = GetConstantValue(field.Constant.Value, false);

                enumFields.Add(new CEnumField(fieldSafeName, fieldValue));
            }

            var fields = enumFields.ToArray();
            builder.AddEnum(safeName, fields);
            return new CType(safeName, false, true, enumFields: fields);
        }

        throw new ArgumentException(null, nameof(type));
    }

    public static CVariable EmitField(
        ref CBuilder builder,
        ref ConcurrentDictionary<string, CType> types,
        FieldDef field)
    {
        var type = GetCType(ref types, field.FieldType);
        var name = Utils.GetSafeName(field.FullName);
        var variable = new CVariable(field.HasConstant, false, type, name);

        if (field.HasConstant) builder.AddVariable(variable, GetConstantValue(field.Constant.Value, true));
        else builder.AddVariable(variable);

        return variable;
    }

    public static void EmitMethodDefinition(
        ref CBuilder builder,
        ref ConcurrentDictionary<string, CType> types,
        MethodDef method)
    {
        var type = GetCType(ref types, method.ReturnType);
        var safeName = Utils.GetSafeName(method.FullName);
        var arguments = new CVariable[method.Parameters.Count];

        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = method.Parameters[i];
            arguments[i] = new CVariable(false, false, GetCType(ref types, argument.Type), argument.Name);
        }

        builder.AddFunction(type, safeName, true, arguments);
    }

    /*
     * This method emits a CIL method into C. Let's see how it works:
     *  1. It creates a C function with the safe name, the return type and the arguments of the method.
     *  2. It creates a list of labels that will later be defined in the function itself. This is required because we
     * need to know where exactly to place the label in the code before attempting to emit any instructions at all.
     *  3. It defines the local variables of the method as C variables. Local variables are the list of variables
     * stored in the local variable list in CIL.
     *  4. It defines the labels that were previously computed.
     *  5. It emits each instruction, one by one. Some of them are completely ignored (like nop), some are partially
     * ignored (like dup where it does nothing at runtime, but it fiddles with the compiler's stack), and all others are
     * emitting runtime instructions. For most instructions, the compiler uses a stack to track the values in what would
     * be the CIL evaluation stack. At runtime (so in the C code), those stack values are actually constant variables,
     * which allows them to be more easily and more efficiently optimized by the C compiler.
     */
    public static void EmitMethod(
        ref CBuilder builder,
        ref ConcurrentDictionary<string, CType> types,
        ref ConcurrentDictionary<string, CVariable> fields,
        MethodDef method)
    {
        var functionType = GetCType(ref types, method.ReturnType);
        var safeName = Utils.GetSafeName(method.FullName);
        var functionArguments = new CVariable[method.Parameters.Count];

        for (var i = 0; i < functionArguments.Length; i++)
        {
            var argument = method.Parameters[i];
            functionArguments[i] = new CVariable(false, false, GetCType(ref types, argument.Type), argument.Name);
        }

        var labels = new Dictionary<uint, string>();
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.OperandType is not (OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)) continue;

            var targetInstruction = (Instruction)instruction.Operand;
            var label = $"IL_{targetInstruction.Offset:X4}";

            labels.Add(targetInstruction.Offset, label);
        }

        builder.AddFunction(functionType, safeName, false, functionArguments);
        builder.BeginBlock();

        var variables = new List<CVariable>(method.Body.Variables.Count); 
        var stackVariables = new Stack<CVariable>(method.Body.MaxStack); 
        var stackVariableCount = 0U;

        builder.AddComment("Locals");
        for (var i = 0; i < method.Body.Variables.Count; i++)
        {
            var local = method.Body.Variables[i];
            var type = GetCType(ref types, local.Type);
            var name = string.IsNullOrEmpty(local.Name) ? $"local{i}" : local.Name;
            var variable = new CVariable(false, false, type, name);

            if (method.Body.InitLocals) builder.AddVariable(variable, GetDefaultValue(type));
            else builder.AddVariable(variable);

            variables.Add(variable);
        }

        foreach (var instruction in method.Body.Instructions)
        {
            builder.AddComment(instruction.ToString()!);

            foreach (var label in labels)
            {
                if (label.Key == instruction.Offset) builder.AddLabel(label.Value);
            }

            switch (instruction.OpCode.Code)
            {
                case Code.Nop: break;
                case Code.Dup: stackVariables.Push(stackVariables.Peek()); break;
                case Code.Pop: stackVariables.Pop(); break;
                case Code.Ldarg_S:
                case Code.Ldarg: EmitLdarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[((Parameter)instruction.Operand).Index]); break;
                case Code.Ldarg_0: EmitLdarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[0]); break;
                case Code.Ldarg_1: EmitLdarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[1]); break;
                case Code.Ldarg_2: EmitLdarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[2]); break;
                case Code.Ldarg_3: EmitLdarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[3]); break;
                case Code.Starg_S:
                case Code.Starg: EmitStarg(ref builder, ref stackVariables, ref stackVariableCount, functionArguments[((Parameter)instruction.Operand).Index]); break;
                case Code.Ldsfld: EmitLdsfld(ref fields, ref stackVariables, (FieldDef)instruction.Operand); break;
                case Code.Stsfld: EmitStsfld(ref fields, ref builder, ref stackVariables, ref stackVariableCount, (FieldDef)instruction.Operand); break;
                case Code.Stfld: EmitStfld(ref types, ref builder, ref stackVariables, ref stackVariableCount, (FieldDef)instruction.Operand); break;
                case Code.Call: EmitCall(ref types, ref builder, ref stackVariables, ref stackVariableCount, (MethodDef)instruction.Operand); break;
                case Code.Ret: EmitRet(ref builder, ref stackVariables, ref stackVariableCount); break;
                case Code.Add: EmitBinaryOperation(ref builder, ref stackVariables, ref stackVariableCount, CBinaryOperator.Add); break;
                case Code.Sub: EmitBinaryOperation(ref builder, ref stackVariables, ref stackVariableCount, CBinaryOperator.Sub); break;
                case Code.Mul: EmitBinaryOperation(ref builder, ref stackVariables, ref stackVariableCount, CBinaryOperator.Mul); break;
                case Code.Div_Un:
                case Code.Div: EmitBinaryOperation(ref builder, ref stackVariables, ref stackVariableCount, CBinaryOperator.Div); break;
                case Code.Rem_Un:
                case Code.Rem: EmitBinaryOperation(ref builder, ref stackVariables, ref stackVariableCount, CBinaryOperator.Mod); break;
                case Code.Ldc_I4_S:
                case Code.Ldc_I4: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, new CConstantInt(Convert.ToInt32(instruction.Operand))); break;
                case Code.Ldc_I4_M1: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.IntM1); break;
                case Code.Ldc_I4_0: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int0); break;
                case Code.Ldc_I4_1: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int1); break;
                case Code.Ldc_I4_2: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int2); break;
                case Code.Ldc_I4_3: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int3); break;
                case Code.Ldc_I4_4: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int4); break;
                case Code.Ldc_I4_5: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int5); break;
                case Code.Ldc_I4_6: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int6); break;
                case Code.Ldc_I4_7: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int7); break;
                case Code.Ldc_I4_8: EmitLdcI4(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int8); break;
                case Code.Ldc_I8: EmitLdcI8(ref builder, ref stackVariables, ref stackVariableCount, new CConstantLong(Convert.ToInt64(instruction.Operand))); break;
                case Code.Conv_I: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.IntPtr); break;
                case Code.Conv_I1: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.SByte); break;
                case Code.Conv_I2: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int16); break;
                case Code.Conv_I4: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int32); break;
                case Code.Conv_I8: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.Int64); break;
                case Code.Conv_U: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.UIntPtr); break;
                case Code.Conv_U1: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Conv_U2: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Conv_U4: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Conv_U8: EmitConv(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Ldloca_S:
                case Code.Ldloca: EmitLdloca(ref builder, ref stackVariables, ref stackVariableCount, variables[((Local)instruction.Operand).Index]); break;
                case Code.Ldloc_S:
                case Code.Ldloc: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, variables[((Local)instruction.Operand).Index]); break;
                case Code.Ldloc_0: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, variables[0]); break;
                case Code.Ldloc_1: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, variables[1]); break;
                case Code.Ldloc_2: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, variables[2]); break;
                case Code.Ldloc_3: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, variables[3]); break;
                case Code.Stloc_S:
                case Code.Stloc: EmitStloc(ref builder, ref stackVariables, ref stackVariableCount, variables[((Local)instruction.Operand).Index]); break;
                case Code.Stloc_0: EmitStloc(ref builder, ref stackVariables, ref stackVariableCount, variables[0]); break;
                case Code.Stloc_1: EmitStloc(ref builder, ref stackVariables, ref stackVariableCount, variables[1]); break;
                case Code.Stloc_2: EmitStloc(ref builder, ref stackVariables, ref stackVariableCount, variables[2]); break;
                case Code.Stloc_3: EmitStloc(ref builder, ref stackVariables, ref stackVariableCount, variables[3]); break;
                case Code.Stobj: EmitStobj(ref builder, ref stackVariables, (TypeDef)instruction.Operand); break;
                // The documentation says ldind.i and stind operands are signed, but that makes no sense so we're making them unsigned
                case Code.Ldind_I1:
                case Code.Ldind_U1: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Ldind_I2:
                case Code.Ldind_U2: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Ldind_I4:
                case Code.Ldind_U4: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Ldind_I8: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Stind_I1: EmitStind(ref builder, ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Stind_I2: EmitStind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Stind_I4: EmitStind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Stind_I8: EmitStind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Ceq: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Equal); break;
                case Code.Cgt_Un:
                case Code.Cgt: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Above); break;
                case Code.Clt_Un:
                case Code.Clt: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Below); break;
                case Code.Br_S:
                case Code.Br: EmitBr(ref builder, (Instruction)instruction.Operand); break;
                case Code.Brtrue_S:
                case Code.Brtrue: EmitCondBrEqual(ref builder, ref stackVariables, (Instruction)instruction.Operand, Utils.BoolTrue); break;
                case Code.Brfalse_S:
                case Code.Brfalse: EmitCondBrEqual(ref builder, ref stackVariables, (Instruction)instruction.Operand, Utils.BoolFalse); break;
                case Code.Beq_S:
                case Code.Beq: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Equal); break;
                case Code.Bge_S:
                case Code.Bge_Un_S:
                case Code.Bge_Un:
                case Code.Bge: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.AboveOrEqual); break;
                case Code.Bgt_S:
                case Code.Bgt_Un_S:
                case Code.Bgt_Un:
                case Code.Bgt: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Above); break;
                case Code.Ble_S:
                case Code.Ble_Un_S:
                case Code.Ble_Un:
                case Code.Ble: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.BelowOrEqual); break;
                case Code.Blt_S:
                case Code.Blt_Un_S:
                case Code.Blt_Un:
                case Code.Blt: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Below); break;
                case Code.Bne_Un_S:
                case Code.Bne_Un: EmitCmpBr(ref builder, ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.NotEqual); break;
                case Code.Sizeof: EmitSizeof(ref builder, ref stackVariables, ref stackVariableCount, (TypeDef)instruction.Operand); break;
                default:
                {
                    Console.WriteLine($"Unimplemented opcode: {instruction.OpCode}");
                    break;
                }
            }
        }

        builder.EndBlock();
    }

    /*
     * This method emits the C entry point of the program. It should call all static constructors, then call the CIL
     * program's entry point.
     */
    public static void EmitMainFunction(
        ref CBuilder builder,
        MethodDef entryPoint,
        ref ConcurrentBag<MethodDef> staticConstructors)
    {
        builder.AddFunction(CType.Int32, "main", false);
        builder.BeginBlock();

        // Call static constructors
        foreach (var method in staticConstructors) builder.AddCall(new CCall(Utils.GetSafeName(method.FullName)));

        // Call entry point
        builder.AddCall(new CCall(Utils.GetSafeName(entryPoint.FullName)));

        builder.AddReturn(Utils.Int0);
        builder.EndBlock();
    }

    #region Helpers

    private static CType GetCType(ref ConcurrentDictionary<string, CType> types, TypeSig type)
    {
        var name = type.FullName;

        // The documentation says pointers are native int, but that doesn't make sense since addresses can't be unsigned
        if (name.EndsWith('*') || name.EndsWith("[]")) return Utils.UIntPtr;
        if (types.TryGetValue(name, out var value)) return value;

        throw new ArgumentOutOfRangeException(nameof(name), name, null);
    }

    private static CType GetBinaryNumericOperationType(CType type1, CType type2)
    {
        if (type1 == Utils.Int32 && type2 == Utils.Int32) return Utils.Int32;
        if (type1 == Utils.Int32 && type2 == Utils.IntPtr) return Utils.IntPtr;
        if (type1 == Utils.Int64 && type2 == Utils.Int64) return Utils.Int64;
        if ((type1 == Utils.IntPtr && type2 == Utils.Int32) || (type1 == Utils.IntPtr && type2 == Utils.IntPtr)) return Utils.IntPtr;
        return Utils.Int32;
    }

    private static CExpression GetDefaultValue(CType type)
    {
        if (type == Utils.Boolean) return CreateStruct(new CConstantBool(false));
        if (type == Utils.SByte
            || type == Utils.Int16
            || type == Utils.Int32
            || type == Utils.Int64
            || type == Utils.Byte
            || type == Utils.UInt16
            || type == Utils.UInt32
            || type == Utils.UInt64
            || type == Utils.IntPtr
            || type == Utils.UIntPtr
            ) return CreateStruct(new CConstantInt(0));

        if (!type.IsStruct || type.StructFields is null) throw new ArgumentOutOfRangeException(nameof(type), type, null);

        var values = new Dictionary<string, CExpression>();
        foreach (var field in type.StructFields) values.Add(field.Name, GetDefaultValue(field.Type));

        var structValue = new CStructInitialization(values);
        return new CBlock(structValue);
    }

    private static CExpression GetConstantValue(object value, bool createStruct) => value switch
    {
        bool u1 => createStruct ? CreateStruct(new CConstantBool(u1)) : new CConstantBool(u1),
        int i32 => createStruct ? CreateStruct(new CConstantInt(i32)) : new CConstantInt(i32),
        byte u8 => createStruct ? CreateStruct(new CConstantInt(u8)) : new CConstantInt(u8),
        long i64 => createStruct ? CreateStruct(new CConstantLong(i64)) : new CConstantLong(i64),
        // TODO
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    private static CExpression CreateStruct(CExpression value)
    {
        var values = new Dictionary<string, CExpression>
        {
            {"value", value}
        };
        var structValue = new CStructInitialization(values);

        return new CBlock(structValue);
    }

    private static string NewStackVariableName(ref uint stackVariableCount) => $"stack{stackVariableCount++}";

    #endregion

    #region Instructions

    private static void EmitBinaryOperation(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CBinaryOperator op)
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var type = op switch
        {
            CBinaryOperator.Add
                or CBinaryOperator.Sub
                or CBinaryOperator.Mul
                or CBinaryOperator.Div
                or CBinaryOperator.Mod
                => GetBinaryNumericOperationType(value1.Type, value2.Type),
            // TODO
            CBinaryOperator.And => throw new NotImplementedException(),
            CBinaryOperator.Or => throw new NotImplementedException(),
            CBinaryOperator.Xor => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
        var result = new CBinaryOperation(actualValue1, op, actualValue2);
        var variable = new CVariable(true, false, type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(result));
        stackVariables.Push(variable);
    }

    private static void EmitLdcI4(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CConstantInt value)
    {
        var variable = new CVariable(true, false, Utils.Int32, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(value));
        stackVariables.Push(variable);
    }

    private static void EmitLdcI8(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CConstantLong value)
    {
        var variable = new CVariable(true, false, Utils.Int64, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, value);
        stackVariables.Push(variable);
    }

    private static void EmitLdloca(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable localVariable)
    {
        var addressOf = new CAddressOf(localVariable);
        var cast = new CCast(true, false, CType.UIntPtr, addressOf);
        var variable = new CVariable(true, false, Utils.UIntPtr, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(cast));
        stackVariables.Push(variable);
    }

    private static void EmitLdloc(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable localVariable)
    {
        var variable = new CVariable(true, localVariable.IsPointer, localVariable.Type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, localVariable);
        stackVariables.Push(variable);
    }

    private static void EmitLdarg(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable argument)
    {
        var variable = new CVariable(true, argument.IsPointer, argument.Type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, argument);
        stackVariables.Push(variable);
    }

    private static void EmitStarg(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable argument)
    {
        var currentValue = stackVariables.Peek();
        if (currentValue.Type != argument.Type) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, argument.Type);

        var value = stackVariables.Pop();
        builder.SetValueExpression(argument, value);
    }

    private static void EmitLdsfld(
        ref ConcurrentDictionary<string, CVariable> fields,
        ref Stack<CVariable> stackVariables,
        FieldDef field)
    {
        if (!fields.TryGetValue(field.FullName, out var variable)) throw new KeyNotFoundException();

        stackVariables.Push(variable);
    }

    private static void EmitStsfld(
        ref ConcurrentDictionary<string, CVariable> fields,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        FieldDef field)
    {
        if (!fields.TryGetValue(field.FullName, out var variable)) throw new KeyNotFoundException();

        var currentValue = stackVariables.Peek();
        if (currentValue.Type != variable.Type) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, variable.Type);

        var value = stackVariables.Pop();
        builder.SetValueExpression(variable, value);
    }

    private static void EmitStfld(
        ref ConcurrentDictionary<string, CType> types,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        FieldDef field)
    {
        var declaringCType = GetCType(ref types, field.DeclaringType.ToTypeSig());
        var cType = GetCType(ref types, field.FieldType);

        var currentValue = stackVariables.Peek();
        if (currentValue.Type != cType) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, cType);

        var value = stackVariables.Pop();
        var classObject = stackVariables.Pop();

        if (classObject.Type == Utils.UIntPtr) // We have a pointer
        {
            var actualAddress = new CDot(classObject, "value");
            var cast = new CCast(false, true, declaringCType, actualAddress);

            builder.SetValueExpression(new CDot(cast, field.Name, true), value);
        }
        else // We have an object reference (a struct)
        {
            builder.SetValueExpression(new CDot(classObject, field.Name), value);
        }
    }

    private static void EmitCall(
        ref ConcurrentDictionary<string, CType> types,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        MethodDef method)
    {
        var arguments = new CExpression[method.Parameters.Count];
        // We're adding arguments in reverse order because we're popping from the stack (last to first)
        for (var i = arguments.Length - 1; i >= 0; i--)
        {
            var parameter = method.Parameters[i];
            var type = GetCType(ref types, parameter.Type);
            var variable = stackVariables.Peek();

            if (type != variable.Type) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, type);

            arguments[i] = stackVariables.Pop();
        }

        var returnType = GetCType(ref types, method.ReturnType);
        var call = new CCall(Utils.GetSafeName(method.FullName), arguments);

        if (returnType != Utils.Void)
        {
            var variable = new CVariable(true, false, returnType, NewStackVariableName(ref stackVariableCount));

            builder.AddVariable(variable, call);
            stackVariables.Push(variable);
        } else builder.AddCall(call);
    }

    private static void EmitRet(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount)
    {
        if (stackVariables.Count > 0)
        {
            var value = stackVariables.Pop();
            builder.AddReturn(value);
        }
        else
        {
            var variable = new CVariable(true, false, Utils.Void, NewStackVariableName(ref stackVariableCount));

            builder.AddVariable(variable, new CBlock(null));
            builder.AddReturn(variable);
        }
    }

    private static void EmitStloc(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable localVariable)
    {
        var currentValue = stackVariables.Peek();
        if (localVariable.Type != currentValue.Type) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, localVariable.Type);

        var value = stackVariables.Pop();
        builder.SetValueExpression(localVariable, value);
    }

    private static void EmitStobj(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        TypeDef classType)
    {
        var value = stackVariables.Pop();
        var address = stackVariables.Pop();
        var valueAddress = new CAddressOf(value);
        var source = new CCast(true, true, CType.Void, valueAddress);
        var actualAddress = new CDot(address, "value");
        var destination = new CCast(false, true, CType.Void, actualAddress);
        var size = new CSizeOf(Utils.GetSafeName(classType.FullName));

        builder.AddCall(new CCall("memcpy", destination, source, size));
    }

    private static void EmitConv(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CType type)
    {
        var value = stackVariables.Pop();
        var actualValue = new CDot(value, "value");
        var variable = new CVariable(true, false, type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(actualValue));
        stackVariables.Push(variable);
    }

    private static void EmitLdind(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CType type)
    {
        var address = stackVariables.Pop();
        var actualAddress = new CDot(address, "value");
        var pointer = new CPointer(new CCast(false, true, type, actualAddress));
        var variable = new CVariable(true, false, type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, pointer);
        stackVariables.Push(variable);
    }

    private static void EmitStind(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CType type)
    {
        var currentValue = stackVariables.Peek();
        if (currentValue.Type != type) EmitConv(ref builder, ref stackVariables, ref stackVariableCount, type);

        var value = stackVariables.Pop();
        var address = stackVariables.Pop();
        var actualAddress = new CDot(address, "value");
        var pointer = new CPointer(new CCast(false, true, type, actualAddress));

        builder.SetValueExpression(pointer, value);
    }

    private static void EmitCmp(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CCompareOperator op)
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var result = new CCompareOperation(actualValue1, op, actualValue2);
        var variable = new CVariable(true, false, Utils.Boolean, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(result));
        stackVariables.Push(variable);
    }

    private static void EmitBr(
        ref CBuilder builder,
        Instruction instruction) => builder.GoToLabel($"IL_{instruction.Offset:X4}");

    private static void EmitCondBrEqual(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        Instruction targetInstruction,
        CExpression compareValue)
    {
        var value = stackVariables.Pop();
        var actualValue = new CDot(value, "value");
        var compare = new CCompareOperation(actualValue, CCompareOperator.Equal, compareValue);

        builder.AddIf(compare);
        builder.BeginBlock();
        builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
        builder.EndBlock();
    }

    private static void EmitCmpBr(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        Instruction targetInstruction,
        CCompareOperator op)
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var compare = new CCompareOperation(actualValue1, op, actualValue2);

        builder.AddIf(compare);
        builder.BeginBlock();
        builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
        builder.EndBlock();
    }

    private static void EmitSizeof(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        TypeDef type)
    {
        var sizeOf = new CSizeOf(Utils.GetSafeName(type.FullName));
        var variable = new CVariable(true, false, Utils.UInt32, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(sizeOf));
        stackVariables.Push(variable);
    }

    #endregion
}