using CCG;
using CCG.Builders;
using CCG.Expressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CIL2C;

public class Emitter
{
    private readonly CBuilder _builder;

    public Emitter(bool minify, bool enableComments)
        => _builder = minify ? new CMinifiedBuilder(enableComments) : new CBeautifiedBuilder(enableComments);

    public override string? ToString() => _builder.ToString();

    public void EmitType(TypeDef type, out TypeSig signature)
    {
        signature = type.ToTypeSig();

        var name = type.FullName;
        var safeName = Utils.GetSafeName(name);

        if (type is { IsClass: true, IsEnum: false })
        {
            _builder.AddStruct(safeName);
            _builder.BeginBlock();

            /*if (type is { IsClass: true, IsValueType: false })
            {
                _builder.AddVariable(new CVariable(false, false, Utils.Object, "header"));
            }*/

            switch (name)
            {
                case "System.Char": _builder.AddVariable(new CVariable(false, false, CType.UInt16, "value")); break;
                case "System.Boolean": _builder.AddVariable(new CVariable(false, false, CType.Boolean, "value")); break;
                case "System.SByte": _builder.AddVariable(new CVariable(false, false, CType.Int8, "value")); break;
                case "System.Int16": _builder.AddVariable(new CVariable(false, false, CType.Int16, "value")); break;
                case "System.Int32": _builder.AddVariable(new CVariable(false, false, CType.Int32, "value")); break;
                case "System.Int64": _builder.AddVariable(new CVariable(false, false, CType.Int64, "value")); break;
                case "System.Byte": _builder.AddVariable(new CVariable(false, false, CType.UInt8, "value")); break;
                case "System.UInt16": _builder.AddVariable(new CVariable(false, false, CType.UInt16, "value")); break;
                case "System.UInt32": _builder.AddVariable(new CVariable(false, false, CType.UInt32, "value")); break;
                case "System.UInt64": _builder.AddVariable(new CVariable(false, false, CType.UInt64, "value")); break;
                case "System.IntPtr": _builder.AddVariable(new CVariable(false, false, CType.IntPtr, "value")); break;
                case "System.UIntPtr": _builder.AddVariable(new CVariable(false, false, CType.UIntPtr, "value")); break;
            }

            _builder.EndBlock();

            Utils.Types.Add(name, new CType(safeName, true));
        }
        else if (type.IsEnum)
        {
            var enumFields = new List<CEnumField>();

            foreach (var field in type.Fields)
            {
                if (field.FieldType.FullName != signature.FullName) continue;

                var fieldSafeName = Utils.GetSafeName(field.FullName);
                var fieldValue = GetConstantValue(field.Constant.Value);

                enumFields.Add(new CEnumField(fieldSafeName, fieldValue));
            }

            _builder.AddEnum(safeName, enumFields.ToArray());

            Utils.Types.Add(name, new CType(safeName, false, true));
        }
    }

    public void EmitField(FieldDef field)
    {
        var type = Utils.GetCType(field.FieldType);
        var name = Utils.GetSafeName(field.FullName);
        var variable = new CVariable(field.HasConstant, false, type, name);

        if (field.HasConstant)
        {
            var constantValue = GetConstantValue(field.Constant.Value);
            if (type.IsStruct)
            {
                var values = new Dictionary<string, CExpression>
                {
                    {"value", constantValue}
                };
                var structValue = new CStructInitialization(values);
                var value = new CBlock(structValue);

                _builder.AddVariable(variable, value);
            } else _builder.AddVariable(variable, constantValue);
        }
        else _builder.AddVariable(variable);

        Utils.Fields.Add(field.FullName, variable);
    }

    public void EmitPrototype(MethodDef method, out CType type, out string safeName, out CVariable[] arguments)
    {
        type = Utils.GetCType(method.ReturnType);
        safeName = Utils.GetSafeName(method.FullName);
        arguments = new CVariable[method.Parameters.Count];

        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = method.Parameters[i];
            arguments[i] = new CVariable(false, false, Utils.GetCType(argument.Type), argument.Name);
        }

        _builder.AddFunction(type, safeName, true, arguments);
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
    public void Emit(MethodDef method, CType functionType, string safeName, CVariable[] functionArguments)
    {
        var labels = new Dictionary<uint, string>();
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.OperandType is not (OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)) continue;

            var targetInstruction = (Instruction)instruction.Operand;
            var label = $"IL_{targetInstruction.Offset:X4}";

            labels.Add(targetInstruction.Offset, label);
        }

        _builder.AddFunction(functionType, safeName, false, functionArguments);
        _builder.BeginBlock();

        var variables = new List<CVariable>(method.Body.Variables.Count); 
        var stackVariables = new Stack<CVariable>(method.Body.MaxStack); 
        var stackVariableCount = 0U;

        _builder.AddComment("Locals");
        // TODO: Init locals
        for (var i = 0; i < method.Body.Variables.Count; i++)
        {
            var local = method.Body.Variables[i];
            var name = string.IsNullOrEmpty(local.Name) ? $"local{i}" : local.Name;
            var variable = new CVariable(false, false, Utils.GetCType(local.Type), name);

            _builder.AddVariable(variable);
            variables.Add(variable);
        }

        foreach (var instruction in method.Body.Instructions)
        {
            _builder.AddComment(instruction.ToString()!);

            foreach (var label in labels)
            {
                if (label.Key == instruction.Offset) _builder.AddLabel(label.Value);
            }

            switch (instruction.OpCode.Code)
            {
                case Code.Nop: break;
                case Code.Dup: stackVariables.Push(stackVariables.Peek()); break;
                case Code.Ldarg: EmitLdarg(ref stackVariables, ref stackVariableCount, functionArguments[Convert.ToUInt16(instruction.Operand)]); break;
                case Code.Ldarg_0: EmitLdarg(ref stackVariables, ref stackVariableCount, functionArguments[0]); break;
                case Code.Ldarg_1: EmitLdarg(ref stackVariables, ref stackVariableCount, functionArguments[1]); break;
                case Code.Ldarg_2: EmitLdarg(ref stackVariables, ref stackVariableCount, functionArguments[2]); break;
                case Code.Ldarg_3: EmitLdarg(ref stackVariables, ref stackVariableCount, functionArguments[3]); break;
                case Code.Ldsfld:
                {
                    var targetField = (FieldDef)instruction.Operand;
                    if (!Utils.Fields.TryGetValue(targetField.FullName, out var variable)) throw new KeyNotFoundException();

                    stackVariables.Push(variable);
                    break;
                }
                case Code.Stsfld:
                {
                    var targetField = (FieldDef)instruction.Operand;
                    if (!Utils.Fields.TryGetValue(targetField.FullName, out var variable)) throw new KeyNotFoundException();

                    var currentValue = stackVariables.Peek();
                    if (currentValue.Type != variable.Type) EmitConv(ref stackVariables, ref stackVariableCount, variable.Type);

                    var value = stackVariables.Pop();
                    _builder.SetValueExpression(variable, value);
                    break;
                }
                case Code.Call:
                {
                    var targetMethod = (MethodDef)instruction.Operand;

                    var arguments = new CExpression[targetMethod.Parameters.Count];
                    // We're adding arguments in reverse order because we're popping from the stack (last to first)
                    for (var i = arguments.Length - 1; i >= 0; i--)
                    {
                        var parameter = targetMethod.Parameters[i];
                        var type = Utils.GetCType(parameter.Type);
                        var variable = stackVariables.Peek();

                        if (type != variable.Type) EmitConv(ref stackVariables, ref stackVariableCount, type);

                        arguments[i] = stackVariables.Pop();
                    }

                    var returnType = Utils.GetCType(targetMethod.ReturnType);
                    var call = new CCall(Utils.GetSafeName(targetMethod.FullName), arguments);

                    if (returnType != Utils.Void)
                    {
                        var variable = new CVariable(true, false, returnType, NewStackVariableName(ref stackVariableCount));

                        _builder.AddVariable(variable, call);
                        stackVariables.Push(variable);
                    } else _builder.AddCall(call);
                    break;
                }
                case Code.Ret:
                {
                    if (stackVariables.Count > 0)
                    {
                        var value = stackVariables.Pop();
                        _builder.AddReturn(value);
                    }
                    else
                    {
                        var variable = new CVariable(true, false, Utils.Void, NewStackVariableName(ref stackVariableCount));

                        _builder.AddVariable(variable, new CBlock(null));
                        _builder.AddReturn(variable);
                    }
                    break;
                }
                case Code.Add: EmitBinaryOperation(ref stackVariables, ref stackVariableCount, CBinaryOperator.Add); break;
                case Code.Sub: EmitBinaryOperation(ref stackVariables, ref stackVariableCount, CBinaryOperator.Sub); break;
                case Code.Mul: EmitBinaryOperation(ref stackVariables, ref stackVariableCount, CBinaryOperator.Mul); break;
                case Code.Div_Un:
                case Code.Div: EmitBinaryOperation(ref stackVariables, ref stackVariableCount, CBinaryOperator.Div); break;
                case Code.Rem_Un:
                case Code.Rem: EmitBinaryOperation(ref stackVariables, ref stackVariableCount, CBinaryOperator.Mod); break;
                case Code.Ldc_I4_S:
                case Code.Ldc_I4: EmitLdcI4(ref stackVariables, ref stackVariableCount, new CConstantInt(Convert.ToInt32(instruction.Operand))); break;
                case Code.Ldc_I4_M1: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.IntM1); break;
                case Code.Ldc_I4_0: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int0); break;
                case Code.Ldc_I4_1: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int1); break;
                case Code.Ldc_I4_2: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int2); break;
                case Code.Ldc_I4_3: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int3); break;
                case Code.Ldc_I4_4: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int4); break;
                case Code.Ldc_I4_5: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int5); break;
                case Code.Ldc_I4_6: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int6); break;
                case Code.Ldc_I4_7: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int7); break;
                case Code.Ldc_I4_8: EmitLdcI4(ref stackVariables, ref stackVariableCount, Utils.Int8); break;
                case Code.Ldc_I8:
                {
                    var value = new CConstantLong(Convert.ToInt64(instruction.Operand));
                    var variable = new CVariable(true, false, Utils.Int64, NewStackVariableName(ref stackVariableCount));

                    _builder.AddVariable(variable, value);
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Conv_I: EmitConv(ref stackVariables, ref stackVariableCount, Utils.IntPtr); break;
                case Code.Conv_I1: EmitConv(ref stackVariables, ref stackVariableCount, Utils.SByte); break;
                case Code.Conv_I2: EmitConv(ref stackVariables, ref stackVariableCount, Utils.Int16); break;
                case Code.Conv_I4: EmitConv(ref stackVariables, ref stackVariableCount, Utils.Int32); break;
                case Code.Conv_I8: EmitConv(ref stackVariables, ref stackVariableCount, Utils.Int64); break;
                case Code.Conv_U: EmitConv(ref stackVariables, ref stackVariableCount, Utils.UIntPtr); break;
                case Code.Conv_U1: EmitConv(ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Conv_U2: EmitConv(ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Conv_U4: EmitConv(ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Conv_U8: EmitConv(ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Ldloc: EmitLdloc(ref stackVariables, ref stackVariableCount, variables[((Local)instruction.Operand).Index]); break;
                case Code.Ldloc_0: EmitLdloc(ref stackVariables, ref stackVariableCount, variables[0]); break;
                case Code.Ldloc_1: EmitLdloc(ref stackVariables, ref stackVariableCount, variables[1]); break;
                case Code.Ldloc_2: EmitLdloc(ref stackVariables, ref stackVariableCount, variables[2]); break;
                case Code.Ldloc_3: EmitLdloc(ref stackVariables, ref stackVariableCount, variables[3]); break;
                case Code.Stloc: EmitStloc(ref stackVariables, ref stackVariableCount, variables[((Local)instruction.Operand).Index]); break;
                case Code.Stloc_0: EmitStloc(ref stackVariables, ref stackVariableCount, variables[0]); break;
                case Code.Stloc_1: EmitStloc(ref stackVariables, ref stackVariableCount, variables[1]); break;
                case Code.Stloc_2: EmitStloc(ref stackVariables, ref stackVariableCount, variables[2]); break;
                case Code.Stloc_3: EmitStloc(ref stackVariables, ref stackVariableCount, variables[3]); break;
                // The documentation says Stind operands are signed, but that makes no sense so we're making them unsigned
                case Code.Stind_I1: EmitStind(ref stackVariables, ref stackVariableCount, Utils.Byte); break;
                case Code.Stind_I2: EmitStind(ref stackVariables, ref stackVariableCount, Utils.UInt16); break;
                case Code.Stind_I4: EmitStind(ref stackVariables, ref stackVariableCount, Utils.UInt32); break;
                case Code.Stind_I8: EmitStind(ref stackVariables, ref stackVariableCount, Utils.UInt64); break;
                case Code.Ceq: EmitCmp(ref stackVariables, ref stackVariableCount, CCompareOperator.Equal); break;
                case Code.Cgt_Un:
                case Code.Cgt: EmitCmp(ref stackVariables, ref stackVariableCount, CCompareOperator.Above); break;
                case Code.Clt_Un:
                case Code.Clt: EmitCmp(ref stackVariables, ref stackVariableCount, CCompareOperator.Below); break;
                case Code.Br_S:
                case Code.Br:
                {
                    var targetInstruction = (Instruction)instruction.Operand;
                    _builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
                    break;
                }
                case Code.Brtrue_S:
                case Code.Brtrue: EmitCondBrEqual(ref stackVariables, (Instruction)instruction.Operand, Utils.BoolTrue); break;
                case Code.Brfalse_S:
                case Code.Brfalse: EmitCondBrEqual(ref stackVariables, (Instruction)instruction.Operand, Utils.BoolFalse); break;
                case Code.Beq_S:
                case Code.Beq: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Equal); break;
                case Code.Bge_S:
                case Code.Bge_Un_S:
                case Code.Bge_Un:
                case Code.Bge: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.AboveOrEqual); break;
                case Code.Bgt_S:
                case Code.Bgt_Un_S:
                case Code.Bgt_Un:
                case Code.Bgt: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Above); break;
                case Code.Ble_S:
                case Code.Ble_Un_S:
                case Code.Ble_Un:
                case Code.Ble: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.BelowOrEqual); break;
                case Code.Blt_S:
                case Code.Blt_Un_S:
                case Code.Blt_Un:
                case Code.Blt: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.Below); break;
                case Code.Bne_Un_S:
                case Code.Bne_Un: EmitCmpBr(ref stackVariables, (Instruction)instruction.Operand, CCompareOperator.NotEqual); break;
                default:
                {
                    Console.WriteLine($"Unimplemented opcode: {instruction.OpCode}");
                    break;
                }
            }
        }

        _builder.EndBlock();
    }

    /*
     * This method emits the C entry point of the program. It should call all static constructors, then call the CIL
     * program's entry point.
     */
    public void EmitMainFunction(MethodDef entryPoint, List<MethodDef> staticConstructors)
    {
        _builder.AddFunction(CType.Int32, "main", false);
        _builder.BeginBlock();

        // Call static constructors
        foreach (var method in staticConstructors) _builder.AddCall(new CCall(Utils.GetSafeName(method.FullName)));

        // Call entry point
        _builder.AddCall(new CCall(Utils.GetSafeName(entryPoint.FullName)));

        _builder.AddReturn(Utils.Int0);
        _builder.EndBlock();
    }

    #region Helpers

    private static CExpression GetConstantValue(object value) => value switch
    {
        bool b => new CConstantBool(b),
        int or byte => new CConstantInt(Convert.ToInt32(value)),
        long l => new CConstantLong(l),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    private void EmitBinaryOperation(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CBinaryOperator op)
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
                => Utils.GetBinaryNumericOperationType(value1.Type, value2.Type),
            CBinaryOperator.And => throw new Exception(),
            CBinaryOperator.Or => throw new Exception(),
            CBinaryOperator.Xor => throw new Exception(),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
        var result = new CBinaryOperation(actualValue1, op, actualValue2);
        var values = new Dictionary<string, CExpression>
        {
            {"value", result}
        };
        var structValue = new CStructInitialization(values);
        var variable = new CVariable(true, false, type, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, new CBlock(structValue));
        stackVariables.Push(variable);
    }

    private void EmitLdcI4(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CConstantInt value)
    {
        var values = new Dictionary<string, CExpression>
        {
            {"value", value}
        };
        var structValue = new CStructInitialization(values);
        var variable = new CVariable(true, false, Utils.Int32, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, new CBlock(structValue));
        stackVariables.Push(variable);
    }

    private void EmitLdloc(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CVariable localVariable)
    {
        var variable = new CVariable(true, localVariable.IsPointer, localVariable.Type, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, localVariable);
        stackVariables.Push(variable);
    }

    private void EmitLdarg(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CVariable argument)
    {
        var variable = new CVariable(true, argument.IsPointer, argument.Type, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, argument);
        stackVariables.Push(variable);
    }

    private void EmitStloc(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CVariable localVariable)
    {
        var currentValue = stackVariables.Peek();
        if (localVariable.Type != currentValue.Type) EmitConv(ref stackVariables, ref stackVariableCount, localVariable.Type);

        var value = stackVariables.Pop();
        _builder.SetValueExpression(localVariable, value);
    }

    private void EmitConv(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CType type)
    {
        var value = stackVariables.Pop();
        var actualValue = new CDot(value, "value");
        var values = new Dictionary<string, CExpression>
        {
            {"value", actualValue}
        };
        var structValue = new CStructInitialization(values);
        var variable = new CVariable(true, false, type, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, new CBlock(structValue));
        stackVariables.Push(variable);
    }

    private void EmitStind(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CType type)
    {
        var currentValue = stackVariables.Peek();
        if (currentValue.Type != type) EmitConv(ref stackVariables, ref stackVariableCount, type);

        var value = stackVariables.Pop();
        var address = stackVariables.Pop();
        var actualAddress = new CDot(address, "value");
        var pointer = new CPointer(new CCast(false, true, type, actualAddress));

        _builder.SetValueExpression(pointer, value);
    }

    private void EmitCmp(ref Stack<CVariable> stackVariables, ref uint stackVariableCount, CCompareOperator op)
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var result = new CCompareOperation(actualValue1, op, actualValue2);
        var values = new Dictionary<string, CExpression>
        {
            {"value", result}
        };
        var structValue = new CStructInitialization(values);
        var variable = new CVariable(true, false, Utils.Boolean, NewStackVariableName(ref stackVariableCount));

        _builder.AddVariable(variable, new CBlock(structValue));
        stackVariables.Push(variable);
    }

    private void EmitCondBrEqual(ref Stack<CVariable> stackVariables, Instruction targetInstruction, CExpression compareValue)
    {
        var value = stackVariables.Pop();
        var actualValue = new CDot(value, "value");
        var compare = new CCompareOperation(actualValue, CCompareOperator.Equal, compareValue);

        _builder.AddIf(compare);
        _builder.BeginBlock();
        _builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
        _builder.EndBlock();
    }

    private void EmitCmpBr(ref Stack<CVariable> stackVariables, Instruction targetInstruction, CCompareOperator op)
    {
        var value2 = stackVariables.Pop();
        var value1 = stackVariables.Pop();
        var actualValue1 = new CDot(value1, "value");
        var actualValue2 = new CDot(value2, "value");
        var compare = new CCompareOperation(actualValue1, op, actualValue2);

        _builder.AddIf(compare);
        _builder.BeginBlock();
        _builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
        _builder.EndBlock();
    }

    private static string NewStackVariableName(ref uint stackVariableCount) => $"stack{stackVariableCount++}";

    #endregion
}