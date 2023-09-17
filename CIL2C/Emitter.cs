using System.Diagnostics;
using CCG;
using CCG.Expressions;
using CIL2C.TypeSystem;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CIL2C;

public static class Emitter
{
    public static void EmitType(
        ref CBuilder builder,
        ref List<string> emittedStructs,
        CilType type
    )
    {
        if (type.IsClass)
        {
            if (emittedStructs.Contains(type.FullName)) return;

            var cBuilder = builder.Clone();
            var structFields = new List<CStructField>();

            if (!type.IsStruct)
            {
                structFields.Add(new CStructField(false, CType.UIntPtr, "methodTable"));
            }

            if (type.FullName == Utils.Char.Name) structFields.Add(new CStructField(false, CType.UInt16, "value"));
            else if (type.FullName == Utils.Boolean.Name) structFields.Add(new CStructField(false, CType.Boolean, "value"));
            else if (type.FullName == Utils.SByte.Name) structFields.Add(new CStructField(false, CType.Int8, "value"));
            else if (type.FullName == Utils.Int16.Name) structFields.Add(new CStructField(false, CType.Int16, "value"));
            else if (type.FullName == Utils.Int32.Name) structFields.Add(new CStructField(false, CType.Int32, "value"));
            else if (type.FullName == Utils.Int64.Name) structFields.Add(new CStructField(false, CType.Int64, "value"));
            else if (type.FullName == Utils.Byte.Name) structFields.Add(new CStructField(false, CType.UInt8, "value"));
            else if (type.FullName == Utils.UInt16.Name) structFields.Add(new CStructField(false, CType.UInt16, "value"));
            else if (type.FullName == Utils.UInt32.Name) structFields.Add(new CStructField(false, CType.UInt32, "value"));
            else if (type.FullName == Utils.UInt64.Name) structFields.Add(new CStructField(false, CType.UInt64, "value"));
            else if (type.FullName == Utils.IntPtr.Name) structFields.Add(new CStructField(false, CType.IntPtr, "value"));
            else if (type.FullName == Utils.UIntPtr.Name) structFields.Add(new CStructField(false, CType.UIntPtr, "value"));

            foreach (var field in type.Fields)
            {
                if (field.Value.IsStatic) continue;
                if (field.Value.Type.IsStruct) EmitType(ref cBuilder, ref emittedStructs, field.Value.Type);

                structFields.Add(new CStructField(false, field.Value.Type.CType, field.Value.Name));
            }

            cBuilder.AddStruct(type.FullName, type.PackStruct, structFields.ToArray());
            emittedStructs.Add(type.FullName);
            builder.Prepend(cBuilder);
            return;
        }

        if (type.IsEnum)
        {
            var enumFields = new List<CEnumField>();

            foreach (var field in type.Fields)
            {
                if (field.Value.Type.FullName != type.FullName) continue;
                if (field.Value.ConstantValue is null) throw new UnreachableException();

                var fieldValue = GetConstantValue(field.Value.ConstantValue, false);
                enumFields.Add(new CEnumField(field.Value.FullName, fieldValue));
            }

            builder.AddEnum(type.FullName, enumFields.ToArray());
            return;
        }

        throw new ArgumentException(null, nameof(type));
    }

    public static void EmitField(
        ref CBuilder builder,
        CilField field
    )
    {
        if (field.ConstantValue is not null) builder.AddVariable(field.Definition, GetConstantValue(field.ConstantValue, true));
        else builder.AddVariable(field.Definition);
    }

    public static void EmitMethodDefinition(
        ref CBuilder builder,
        CilMethod method
    )
    {
        var arguments = new CVariable[method.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = method.Arguments[i];
            arguments[i] = new CVariable(false, false, argument.Type.CType, argument.Name);
        }

        builder.AddFunction(method.ReturnType.CType, method.FullName, true, arguments);
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
        ref CilModule module,
        ref CBuilder builder,
        CilMethod method
    )
    {
        var functionArguments = new CVariable[method.Arguments.Count];
        for (var i = 0; i < functionArguments.Length; i++)
        {
            var argument = method.Arguments[i];
            functionArguments[i] = new CVariable(false, false, argument.Type.CType, argument.Name);
        }

        if (method is { NeedsExternalCFunction: true, ExternalCFunctionName: not null })
        {
            builder.AddFunction(method.ReturnType.CType, method.FullName, false, functionArguments);
            builder.BeginBlock();

            var arguments = new CExpression[functionArguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                var functionArgument = functionArguments[i];
                var cType = ToCType(functionArgument.Type);

                arguments[i] = ConvertValue(functionArgument, cType, false);
            }

            var call = new CCall(method.ExternalCFunctionName, arguments);

            if (method.ReturnType.CType != Utils.Void)
            {
                var value = CreateStruct(call);
                builder.AddReturn(new CCast(true, false, method.ReturnType.CType, value));
            }
            else
            {
                var value = new CBlock(null);

                builder.AddCall(call);
                builder.AddReturn(new CCast(true, false, Utils.Void, value));
            }
            
            builder.EndBlock();
            return;
        }

        if (method.Body is null) throw new UnreachableException();

        var labels = new Dictionary<uint, string>();
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.OperandType is not (OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)) continue;

            var targetInstruction = (Instruction)instruction.Operand;
            var label = $"IL_{targetInstruction.Offset:X4}";

            labels.Add(targetInstruction.Offset, label);
        }

        builder.AddFunction(method.ReturnType.CType, method.FullName, false, functionArguments);
        builder.BeginBlock();

        var stackVariables = new Stack<CVariable>(method.Body.MaxStackSize);
        var stackVariableCount = 0U;

        builder.AddComment("Locals");
        foreach (var local in method.Body.Locals)
        {
            if (method.Body.InitializeLocals) builder.AddVariable(local.Definition, GetDefaultValue(local.Type));
            else builder.AddVariable(local.Definition);
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
                case Code.Starg: EmitStarg(ref builder, ref stackVariables, functionArguments[((Parameter)instruction.Operand).Index]); break;
                case Code.Ldsfld: EmitLdsfld(ref module, ref stackVariables, ((FieldDef)instruction.Operand).FullName); break;
                case Code.Stsfld: EmitStsfld(ref module, ref builder, ref stackVariables, ((FieldDef)instruction.Operand).FullName); break;
                case Code.Ldfld: EmitLdfld(ref module, ref builder, ref stackVariables, ref stackVariableCount, ((FieldDef)instruction.Operand).FullName); break;
                case Code.Stfld: EmitStfld(ref module, ref builder, ref stackVariables, ((FieldDef)instruction.Operand).FullName); break;
                case Code.Call: EmitCall(ref module, ref builder, ref stackVariables, ref stackVariableCount, ((MethodDef)instruction.Operand).FullName); break;
                case Code.Ret: EmitRet(ref builder, ref stackVariables); break;
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
                case Code.Ldloca: EmitLdloca(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[((Local)instruction.Operand).Index]); break;
                case Code.Ldloc_S:
                case Code.Ldloc: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[((Local)instruction.Operand).Index]); break;
                case Code.Ldloc_0: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[0]); break;
                case Code.Ldloc_1: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[1]); break;
                case Code.Ldloc_2: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[2]); break;
                case Code.Ldloc_3: EmitLdloc(ref builder, ref stackVariables, ref stackVariableCount, method.Body.Locals[3]); break;
                case Code.Stloc_S:
                case Code.Stloc: EmitStloc(ref builder, ref stackVariables, method.Body.Locals[((Local)instruction.Operand).Index]); break;
                case Code.Stloc_0: EmitStloc(ref builder, ref stackVariables, method.Body.Locals[0]); break;
                case Code.Stloc_1: EmitStloc(ref builder, ref stackVariables, method.Body.Locals[1]); break;
                case Code.Stloc_2: EmitStloc(ref builder, ref stackVariables, method.Body.Locals[2]); break;
                case Code.Stloc_3: EmitStloc(ref builder, ref stackVariables, method.Body.Locals[3]); break;
                case Code.Stobj: EmitStobj(ref builder, ref stackVariables, Utils.GetSafeName(((TypeDef)instruction.Operand).FullName)); break;
                // The documentation says ldind.i and stind operands are signed, but that makes no sense so we're making them unsigned
                case Code.Ldind_I1:
                case Code.Ldind_U1: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Ldind_I2:
                case Code.Ldind_U2: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Ldind_I4:
                case Code.Ldind_U4: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Ldind_I8: EmitLdind(ref builder, ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Stind_I1: EmitStind(ref builder, ref stackVariables, Utils.Byte); break;
                case Code.Stind_I2: EmitStind(ref builder, ref stackVariables, Utils.UInt16); break;
                case Code.Stind_I4: EmitStind(ref builder, ref stackVariables, Utils.UInt32); break;
                case Code.Stind_I8: EmitStind(ref builder, ref stackVariables, Utils.UInt64); break;
                case Code.Ceq: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Equal); break;
                case Code.Cgt_Un:
                case Code.Cgt: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Above); break;
                case Code.Clt_Un:
                case Code.Clt: EmitCmp(ref builder, ref stackVariables, ref stackVariableCount, CCompareOperator.Below); break;
                case Code.Br_S:
                case Code.Br: EmitBr(ref builder, ((Instruction)instruction.Operand).Offset); break;
                case Code.Brtrue_S:
                case Code.Brtrue: EmitCondBrEqual(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, Utils.BoolTrue); break;
                case Code.Brfalse_S:
                case Code.Brfalse: EmitCondBrEqual(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, Utils.BoolFalse); break;
                case Code.Beq_S:
                case Code.Beq: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.Equal); break;
                case Code.Bge_S:
                case Code.Bge_Un_S:
                case Code.Bge_Un:
                case Code.Bge: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.AboveOrEqual); break;
                case Code.Bgt_S:
                case Code.Bgt_Un_S:
                case Code.Bgt_Un:
                case Code.Bgt: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.Above); break;
                case Code.Ble_S:
                case Code.Ble_Un_S:
                case Code.Ble_Un:
                case Code.Ble: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.BelowOrEqual); break;
                case Code.Blt_S:
                case Code.Blt_Un_S:
                case Code.Blt_Un:
                case Code.Blt: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.Below); break;
                case Code.Bne_Un_S:
                case Code.Bne_Un: EmitCmpBr(ref builder, ref stackVariables, ((Instruction)instruction.Operand).Offset, CCompareOperator.NotEqual); break;
                case Code.Sizeof: EmitSizeof(ref builder, ref stackVariables, ref stackVariableCount, Utils.GetSafeName(((TypeDef)instruction.Operand).FullName)); break;
                case Code.Initobj: EmitInitobj(ref module, ref builder, ref stackVariables, ((TypeDef)instruction.Operand).FullName); break;
                default:
                {
                    Logger.Info($"Unimplemented opcode: {instruction.OpCode}");
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
        ref CilModule module
    )
    {
        builder.AddFunction(CType.Int32, "main", false);
        builder.BeginBlock();

        // Call static constructors
        foreach (var method in module.AllStaticConstructors) builder.AddCall(new CCall(method.Value.FullName));

        // Call entry point
        builder.AddCall(new CCall(module.EntryPoint.FullName));

        builder.AddReturn(Utils.Int0);
        builder.EndBlock();
    }

    #region Helpers

    private static CType GetBinaryNumericOperationType(
        CType type1,
        CType type2
    )
    {
        if (type1 == Utils.Int32 && type2 == Utils.Int32) return Utils.Int32;
        if (type1 == Utils.Int32 && type2 == Utils.IntPtr) return Utils.IntPtr;
        if (type1 == Utils.Int64 && type2 == Utils.Int64) return Utils.Int64;
        if ((type1 == Utils.IntPtr && type2 == Utils.Int32) || (type1 == Utils.IntPtr && type2 == Utils.IntPtr)) return Utils.IntPtr;
        return Utils.Int32;
    }

    private static CExpression GetDefaultValue(
        CilType type
    )
    {
        if (type.CType == Utils.Boolean) return CreateStruct(new CConstantBool(false));
        if (type.CType == Utils.SByte
            || type.CType == Utils.Int16
            || type.CType == Utils.Int32
            || type.CType == Utils.Int64
            || type.CType == Utils.Byte
            || type.CType == Utils.UInt16
            || type.CType == Utils.UInt32
            || type.CType == Utils.UInt64
            || type.CType == Utils.IntPtr
            || type.CType == Utils.UIntPtr
            ) return CreateStruct(new CConstantInt(0));

        if (!type.IsStruct) throw new ArgumentOutOfRangeException(nameof(type), type, null);

        var values = new Dictionary<string, CExpression>();
        foreach (var field in type.Fields)
        {
            if (field.Value.IsStatic) continue;
            values.Add(field.Value.Name, GetDefaultValue(field.Value.Type));
        }

        var structValue = new CStructInitialization(values);
        return new CBlock(structValue);
    }

    private static CExpression GetConstantValue(
        object value,
        bool createStruct
    ) => value switch
    {
        bool u1 => createStruct ? CreateStruct(new CConstantBool(u1)) : new CConstantBool(u1),
        ushort u16 => createStruct ? CreateStruct(new CConstantInt(u16)) : new CConstantInt(u16),
        int i32 => createStruct ? CreateStruct(new CConstantInt(i32)) : new CConstantInt(i32),
        byte u8 => createStruct ? CreateStruct(new CConstantInt(u8)) : new CConstantInt(u8),
        long i64 => createStruct ? CreateStruct(new CConstantLong(i64)) : new CConstantLong(i64),
        _ => throw new NotImplementedException()
    };

    private static CExpression CreateStruct(
        CExpression value
    )
    {
        var values = new Dictionary<string, CExpression>
        {
            {"value", value}
        };
        var structValue = new CStructInitialization(values);

        return new CBlock(structValue);
    }

    private static CExpression ConvertValue(
        CExpression value,
        CType type,
        bool createStruct = true
    )
    {
        var actualValue = new CDot(value, "value");

        return createStruct ? new CCast(true, false, type, CreateStruct(actualValue)) : actualValue;
    }

    private static CType ToCType(
        CType cilType
    )
    {
        CType cType;

        if (cilType == Utils.Boolean) cType = CType.Boolean;
        else if (cilType == Utils.SByte) cType = CType.Int8;
        else if (cilType == Utils.Int16) cType = CType.Int16;
        else if (cilType == Utils.Int32) cType = CType.Int32;
        else if (cilType == Utils.Int64) cType = CType.Int64;
        else if (cilType == Utils.Byte) cType = CType.UInt8;
        else if (cilType == Utils.UInt16) cType = CType.UInt16;
        else if (cilType == Utils.UInt32) cType = CType.UInt32;
        else if (cilType == Utils.UInt64) cType = CType.UInt64;
        else if (cilType == Utils.IntPtr) cType = CType.IntPtr;
        else if (cilType == Utils.UIntPtr) cType = CType.UIntPtr;
        else throw new ArgumentOutOfRangeException(null, nameof(cilType));

        return cType;
    }

    private static string NewStackVariableName(
        ref uint stackVariableCount
    ) => $"stack{stackVariableCount++}";

    #endregion

    #region Instructions

    private static void EmitBinaryOperation(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CBinaryOperator op
    )
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
        CConstantInt value
    )
    {
        var variable = new CVariable(true, false, Utils.Int32, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(value));
        stackVariables.Push(variable);
    }

    private static void EmitLdcI8(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CConstantLong value
    )
    {
        var variable = new CVariable(true, false, Utils.Int64, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, value);
        stackVariables.Push(variable);
    }

    private static void EmitLdloca(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CilLocal localVariable
    )
    {
        var addressOf = new CAddressOf(localVariable.Definition);
        var cast = new CCast(true, false, CType.UIntPtr, addressOf);
        var variable = new CVariable(true, false, Utils.UIntPtr, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(cast));
        stackVariables.Push(variable);
    }

    private static void EmitLdloc(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CilLocal localVariable
    )
    {
        var variable = new CVariable(true, localVariable.Definition.IsPointer, localVariable.Type.CType, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, localVariable.Definition);
        stackVariables.Push(variable);
    }

    private static void EmitLdarg(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CVariable argument
    )
    {
        var variable = new CVariable(true, argument.IsPointer, argument.Type, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, argument);
        stackVariables.Push(variable);
    }

    private static void EmitStarg(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        CVariable argument
    )
    {
        var value = stackVariables.Pop();

        CExpression actualValue = value;
        if (value.Type != argument.Type) actualValue = ConvertValue(value, argument.Type);

        builder.SetValueExpression(argument, actualValue);
    }

    private static void EmitLdsfld(
        ref CilModule module,
        ref Stack<CVariable> stackVariables,
        string fieldName
    )
    {
        if (!module.AllStaticFields.TryGetValue(fieldName, out var cilField)) throw new UnreachableException();

        stackVariables.Push(cilField.Definition);
    }

    private static void EmitStsfld(
        ref CilModule module,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        string fieldName
    )
    {
        if (!module.AllStaticFields.TryGetValue(fieldName, out var cilField)) throw new UnreachableException();

        var value = stackVariables.Pop();

        CExpression actualValue = value;
        if (value.Type != cilField.Type.CType) actualValue = ConvertValue(value, cilField.Type.CType);

        builder.SetValueExpression(cilField.Definition, actualValue);
    }

    private static void EmitLdfld(
        ref CilModule module,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        string fieldName
    )
    {
        if (!module.AllNonStaticFields.TryGetValue(fieldName, out var cilField)) throw new UnreachableException();

        var classObject = stackVariables.Pop();

        if (classObject.Type == Utils.UIntPtr) // We have a pointer
        {
            var actualAddress = new CDot(classObject, "value");
            var cast = new CCast(false, true, cilField.ParentType.CType, actualAddress);
            var variable = new CVariable(true, false, cilField.Type.CType, NewStackVariableName(ref stackVariableCount));

            stackVariables.Push(variable);
            builder.AddVariable(variable, new CDot(cast, cilField.Name, true));
        }
        else // We have an object reference (a struct)
        {
            var variable = new CVariable(true, false, cilField.Type.CType, NewStackVariableName(ref stackVariableCount));

            stackVariables.Push(variable);
            builder.AddVariable(variable, new CDot(classObject, cilField.Name));
        }
    }

    private static void EmitStfld(
        ref CilModule module,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        string fieldName
    )
    {
        if (!module.AllNonStaticFields.TryGetValue(fieldName, out var cilField)) throw new UnreachableException();

        var value = stackVariables.Pop();

        CExpression actualValue = value;
        if (value.Type != cilField.Type.CType) actualValue = ConvertValue(value, cilField.Type.CType);

        var classObject = stackVariables.Pop();

        if (classObject.Type == Utils.UIntPtr) // We have a pointer
        {
            var actualAddress = new CDot(classObject, "value");
            var cast = new CCast(false, true, cilField.ParentType.CType, actualAddress);

            builder.SetValueExpression(new CDot(cast, cilField.Name, true), actualValue);
        }
        else // We have an object reference (a struct)
        {
            builder.SetValueExpression(new CDot(classObject, cilField.Name), actualValue);
        }
    }

    private static void EmitCall(
        ref CilModule module,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        string methodName
    )
    {
        if (!module.AllMethods.TryGetValue(methodName, out var cilMethod)) throw new UnreachableException();

        var arguments = new CExpression[cilMethod.Arguments.Count];
        // We're adding arguments in reverse order because we're popping from the stack (last to first)
        for (var i = arguments.Length - 1; i >= 0; i--)
        {
            var parameter = cilMethod.Arguments[i];
            var value = stackVariables.Pop();

            CExpression actualValue = value;
            if (value.Type != parameter.Type.CType) actualValue = ConvertValue(value, parameter.Type.CType);

            arguments[i] = actualValue;
        }

        var call = new CCall(cilMethod.FullName, arguments);

        if (cilMethod.ReturnType.CType != Utils.Void)
        {
            var variable = new CVariable(true, false, cilMethod.ReturnType.CType, NewStackVariableName(ref stackVariableCount));

            builder.AddVariable(variable, call);
            stackVariables.Push(variable);
        } else builder.AddCall(call);
    }

    private static void EmitRet(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables
    )
    {
        if (stackVariables.Count > 0)
        {
            var value = stackVariables.Pop();
            builder.AddReturn(value);
        }
        else
        {
            var value = new CBlock(null);
            builder.AddReturn(new CCast(true, false, Utils.Void, value));
        }
    }

    private static void EmitStloc(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        CilLocal localVariable
    )
    {
        var value = stackVariables.Pop();

        CExpression actualValue = value;
        if (value.Type != localVariable.Type.CType) actualValue = ConvertValue(value, localVariable.Type.CType);

        builder.SetValueExpression(localVariable.Definition, actualValue);
    }

    private static void EmitStobj(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        string classTypeName
    )
    {
        var value = stackVariables.Pop();
        var address = stackVariables.Pop();
        var valueAddress = new CAddressOf(value);
        var source = new CCast(true, true, CType.Void, valueAddress);
        var actualAddress = new CDot(address, "value");
        var destination = new CCast(false, true, CType.Void, actualAddress);
        var size = new CSizeOf(classTypeName);

        builder.AddCall(new CCall("memcpy", destination, source, size));
    }

    private static void EmitConv(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CType type
    )
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
        CType type
    )
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
        CType type
    )
    {
        var value = stackVariables.Pop();

        CExpression actualValue = value;
        if (value.Type != type) actualValue = ConvertValue(value, type);

        var address = stackVariables.Pop();
        var actualAddress = new CDot(address, "value");
        var pointer = new CPointer(new CCast(false, true, type, actualAddress));

        builder.SetValueExpression(pointer, actualValue);
    }

    private static void EmitCmp(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        CCompareOperator op
    )
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
        uint targetOffset
    ) => builder.GoToLabel($"IL_{targetOffset:X4}");

    private static void EmitCondBrEqual(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        uint targetOffset,
        CExpression compareValue
    )
    {
        var value = stackVariables.Pop();
        var actualValue = new CDot(value, "value");
        var compare = new CCompareOperation(actualValue, CCompareOperator.Equal, compareValue);

        builder.AddIf(compare);
        builder.BeginBlock();
        builder.GoToLabel($"IL_{targetOffset:X4}");
        builder.EndBlock();
    }

    private static void EmitCmpBr(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        uint targetOffset,
        CCompareOperator op
    )
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var compare = new CCompareOperation(actualValue1, op, actualValue2);

        builder.AddIf(compare);
        builder.BeginBlock();
        builder.GoToLabel($"IL_{targetOffset:X4}");
        builder.EndBlock();
    }

    private static void EmitSizeof(
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        ref uint stackVariableCount,
        string typeName
    )
    {
        var sizeOf = new CSizeOf(typeName);
        var variable = new CVariable(true, false, Utils.UInt32, NewStackVariableName(ref stackVariableCount));

        builder.AddVariable(variable, CreateStruct(sizeOf));
        stackVariables.Push(variable);
    }

    private static void EmitInitobj(
        ref CilModule module,
        ref CBuilder builder,
        ref Stack<CVariable> stackVariables,
        string typeName
    )
    {
        var type = module.Types[typeName];
        var address = stackVariables.Pop();
        var actualAddress = new CDot(address, "value");
        var cast = new CCast(false, true, type.CType, actualAddress);
        var value = GetDefaultValue(type);

        builder.SetValueExpression(new CPointer(cast), new CCast(false, false, type.CType, value));
    }

    #endregion
}