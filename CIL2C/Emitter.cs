using CCG;
using CCG.Builders;
using CCG.Expressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CIL2C;

public class Emitter
{
    private readonly CBuilder _builder;
    private readonly List<CVariable> _variables = new();
    private readonly Stack<CVariable> _stackVariables = new();

    private uint _stackVariableCount;

    public Emitter(bool minify, bool enableComments)
        => _builder = minify ? new CMinifiedBuilder(enableComments) : new CBeautifiedBuilder(enableComments);

    public override string? ToString() => _builder.ToString();

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

        _variables.Clear();
        _variables.EnsureCapacity(method.Body.Variables.Count);

        _stackVariables.Clear();
        _stackVariables.EnsureCapacity(method.Body.MaxStack);

        _stackVariableCount = 0;

        _builder.AddComment("Locals");
        // TODO: Init locals
        for (var i = 0; i < method.Body.Variables.Count; i++)
        {
            var local = method.Body.Variables[i];
            var name = string.IsNullOrEmpty(local.Name) ? $"local{i}" : local.Name;
            var variable = new CVariable(false, false, Utils.GetCType(local.Type), name);

            _builder.AddVariable(variable);
            _variables.Add(variable);
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
                case Code.Dup: _stackVariables.Push(_stackVariables.Peek()); break;
                case Code.Ldarg: EmitLdarg(functionArguments[Convert.ToUInt16(instruction.Operand)]); break;
                case Code.Ldarg_0: EmitLdarg(functionArguments[0]); break;
                case Code.Ldarg_1: EmitLdarg(functionArguments[1]); break;
                case Code.Ldarg_2: EmitLdarg(functionArguments[2]); break;
                case Code.Ldarg_3: EmitLdarg(functionArguments[3]); break;
                case Code.Call:
                {
                    var targetMethod = (MethodDef)instruction.Operand;

                    var arguments = new CExpression[targetMethod.Parameters.Count];
                    // We're adding arguments in reverse order because we're popping from the stack (last to first)
                    for (var i = arguments.Length - 1; i >= 0; i--) arguments[i] = _stackVariables.Pop();

                    var returnType = Utils.GetCType(targetMethod.ReturnType);
                    var call = new CCall(Utils.GetSafeName(targetMethod.FullName), arguments);

                    if (returnType != CType.Void)
                    {
                        var variable = new CVariable(true, false, returnType, NewStackVariableName());

                        _builder.AddVariable(variable, call);
                        _stackVariables.Push(variable);
                    } else _builder.AddCall(call);
                    break;
                }
                case Code.Ret:
                {
                    if (_stackVariables.Count > 0)
                    {
                        var value = _stackVariables.Pop();
                        _builder.AddReturn(value);
                    } else _builder.AddReturn();
                    break;
                }
                case Code.Add:
                {
                    var value2 = _stackVariables.Pop();
                    var value1 = _stackVariables.Pop();
                    
                    var type = Utils.GetAddFinalType(value1.Type, value2.Type);
                    var variable = new CVariable(true, false, type, NewStackVariableName());
                    var result = new CCast(true, false, type, new CBinaryOperation(value1, CBinaryOperator.Add, value2));

                    _builder.AddVariable(variable, result);
                    _stackVariables.Push(variable);
                    break;
                }
                case Code.Ldc_I4_S:
                case Code.Ldc_I4: EmitLdcI4(new CConstantInt(Convert.ToInt32(instruction.Operand))); break;
                case Code.Ldc_I4_M1: EmitLdcI4(Utils.IntM1); break;
                case Code.Ldc_I4_0: EmitLdcI4(Utils.Int0); break;
                case Code.Ldc_I4_1: EmitLdcI4(Utils.Int1); break;
                case Code.Ldc_I4_2: EmitLdcI4(Utils.Int2); break;
                case Code.Ldc_I4_3: EmitLdcI4(Utils.Int3); break;
                case Code.Ldc_I4_4: EmitLdcI4(Utils.Int4); break;
                case Code.Ldc_I4_5: EmitLdcI4(Utils.Int5); break;
                case Code.Ldc_I4_6: EmitLdcI4(Utils.Int6); break;
                case Code.Ldc_I4_7: EmitLdcI4(Utils.Int7); break;
                case Code.Ldc_I4_8: EmitLdcI4(Utils.Int8); break;
                case Code.Conv_I: EmitConv(CType.IntPtr); break;
                case Code.Conv_I1: EmitConv(CType.Int8); break;
                case Code.Conv_I2: EmitConv(CType.Int16); break;
                case Code.Conv_I4: EmitConv(CType.Int32); break;
                case Code.Conv_I8: EmitConv(CType.Int64); break;
                case Code.Conv_U: EmitConv(CType.UIntPtr); break;
                case Code.Conv_U1: EmitConv(CType.UInt8); break;
                case Code.Conv_U2: EmitConv(CType.UInt16); break;
                case Code.Conv_U4: EmitConv(CType.UInt32); break;
                case Code.Conv_U8: EmitConv(CType.UInt64); break;
                case Code.Ldloc: EmitLdloc((ushort)((Local)instruction.Operand).Index); break;
                case Code.Ldloc_0: EmitLdloc(0); break;
                case Code.Ldloc_1: EmitLdloc(1); break;
                case Code.Ldloc_2: EmitLdloc(2); break;
                case Code.Ldloc_3: EmitLdloc(3); break;
                case Code.Stloc: EmitStloc((ushort)((Local)instruction.Operand).Index); break;
                case Code.Stloc_0: EmitStloc(0); break;
                case Code.Stloc_1: EmitStloc(1); break;
                case Code.Stloc_2: EmitStloc(2); break;
                case Code.Stloc_3: EmitStloc(3); break;
                // The documentation says Stind operands are signed, but that makes no sense so we're making them unsigned
                case Code.Stind_I1: EmitStind(CType.UInt8); break;
                case Code.Stind_I2: EmitStind(CType.UInt16); break;
                case Code.Stind_I4: EmitStind(CType.UInt32); break;
                case Code.Stind_I8: EmitStind(CType.UInt64); break;
                case Code.Ceq: EmitCmp(CCompareOperator.Equal); break;
                case Code.Cgt_Un:
                case Code.Cgt: EmitCmp(CCompareOperator.Above); break;
                case Code.Clt_Un:
                case Code.Clt: EmitCmp(CCompareOperator.Below); break;
                case Code.Br_S:
                case Code.Br:
                {
                    var targetInstruction = (Instruction)instruction.Operand;
                    _builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
                    break;
                }
                case Code.Brtrue_S:
                case Code.Brtrue: EmitCondBr((Instruction)instruction.Operand, Utils.BoolTrue); break;
                case Code.Brfalse_S:
                case Code.Brfalse: EmitCondBr((Instruction)instruction.Operand, Utils.BoolFalse); break;
                default:
                {
                    Console.WriteLine($"Unimplemented opcode: {instruction.OpCode}");
                    break;
                }
            }
        }

        _builder.EndBlock(true);
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
        _builder.EndBlock(true);
    }

    #region Helpers

    private void EmitLdcI4(CConstantInt value)
    {
        var variable = new CVariable(true, false, CType.Int32, NewStackVariableName());

        _builder.AddVariable(variable, value);
        _stackVariables.Push(variable);
    }

    private void EmitLdloc(ushort index)
    {
        var localVariable = _variables[index];
        var variable = new CVariable(true, localVariable.IsPointer, localVariable.Type, NewStackVariableName());

        _builder.AddVariable(variable, localVariable);
        _stackVariables.Push(variable);
    }

    private void EmitLdarg(CVariable argument)
    {
        var variable = new CVariable(true, argument.IsPointer, argument.Type, NewStackVariableName());

        _builder.AddVariable(variable, argument);
        _stackVariables.Push(variable);
    }

    private void EmitStloc(ushort index)
    {
        var variable = _variables[index];
        var value = _stackVariables.Pop();

        _builder.SetValueExpression(variable, value);
    }

    private void EmitConv(CType type)
    {
        var value = _stackVariables.Pop();
        var variable = new CVariable(true, false, type, NewStackVariableName());

        _builder.AddVariable(variable, new CCast(true, false, variable.Type, value));
        _stackVariables.Push(variable);
    }

    private void EmitStind(CType type)
    {
        var value = _stackVariables.Pop();
        var address = _stackVariables.Pop();
        var pointer = new CPointer(new CCast(false, true, type, address));

        _builder.SetValueExpression(pointer, value);
    }

    private void EmitCondBr(Instruction targetInstruction, CExpression compareValue)
    {
        var value = _stackVariables.Pop();
        var compare = new CCompareOperation(value, CCompareOperator.Equal, compareValue);

        _builder.AddIf(compare);
        _builder.BeginBlock();
        _builder.GoToLabel($"IL_{targetInstruction.Offset:X4}");
        _builder.EndBlock(false);
    }

    private void EmitCmp(CCompareOperator op)
    {
        var value2 = _stackVariables.Pop();
        var value1 = _stackVariables.Pop();
        var compare = new CCompareOperation(value1, op, value2);
        var variable = new CVariable(true, false, CType.Boolean, NewStackVariableName());

        _builder.AddVariable(variable, compare);
        _stackVariables.Push(variable);
    }

    private string NewStackVariableName() => $"stack{_stackVariableCount++}";

    #endregion
}