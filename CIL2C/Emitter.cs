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

    public Emitter(bool minify) => _builder = minify ? new CMinifiedBuilder() : new CBeautifiedBuilder();

    public override string? ToString() => _builder.ToString();

    /*
     * This method emits a CIL method into C. Let's see how it works:
     *  1. It creates a C function with the safe name and the return type of the method.
     *  2. It defines the local variables of the method as C variables. Local variables are the list of variables
     * stored in the local variable list in CIL.
     *  3. It emits each instruction, one by one. Some of them are completely ignored (like nop), some are partially
     * ignored (like dup where it does nothing at runtime, but it fiddles with the compiler's stack), and all others are
     * emitting runtime instructions. For most instructions, the compiler uses a stack to track the values in what would
     * be the CIL evaluation stack. At runtime (so in the C code), those stack values are actually constant variables,
     * which allows them to be more easily and more efficiently optimized by the C compiler.
     */
    public void Emit(MethodDef method)
    {
        _builder.AddFunction(Utils.GetCType(method.ReturnType), Utils.GetSafeName(method.FullName));
        _builder.BeginBlock();

        _variables.EnsureCapacity(method.Body.Variables.Count);

        _builder.AddComment("Locals");
        foreach (var local in method.Body.Variables)
        {
            var variable = new CVariable(false, false, Utils.GetCType(local.Type), local.Name);
            _variables.Add(variable);
            _builder.AddVariable(variable);
        }

        foreach (var instruction in method.Body.Instructions)
        {
            _builder.AddComment(instruction.OpCode.Name);
            switch (instruction.OpCode.Code)
            {
                case Code.Nop: break;
                case Code.Dup:
                {
                    var value = _stackVariables.Peek();
                    _stackVariables.Push(value);
                    break;
                }
                case Code.Ret:
                {
                    // TODO: Check for a return value
                    _builder.AddReturn();
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
                case Code.Ldc_I4_S:
                case Code.Ldc_I4: EmitLdcI4(new CConstantInt(Convert.ToInt32(instruction.Operand))); break;
                case Code.Conv_I:
                {
                    var value = _stackVariables.Pop();
                    var variable = new CVariable(true, false, CType.IntPtr, NewStackVariableName());
                    _builder.AddVariable(variable, new CCast(true, false, variable.Type, value));
                    _stackVariables.Push(variable);
                    break;
                }
                case Code.Ldloc_0: EmitLdloc(0); break;
                case Code.Ldloc_1: EmitLdloc(1); break;
                case Code.Ldloc_2: EmitLdloc(2); break;
                case Code.Ldloc_3: EmitLdloc(3); break;
                case Code.Stloc_0: EmitStloc(0); break;
                case Code.Stloc_1: EmitStloc(1); break;
                case Code.Stloc_2: EmitStloc(2); break;
                case Code.Stloc_3: EmitStloc(3); break;
                case Code.Stind_I1:
                {
                    var value = _stackVariables.Pop();
                    var address = _stackVariables.Pop();
                    var pointer = new CPointer(new CCast(false, true, CType.UInt8, address));
                    _builder.SetValueExpression(pointer, value);
                    break;
                }
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
     * TODO: Call static constructors
     * This method emits the C entry point of the program. It should call all static constructors, then call the CIL
     * program's entry point.
     */
    public void EmitMainFunction(MethodDef entryPoint)
    {
        _builder.AddFunction(CType.Int32, "main");
        _builder.BeginBlock();
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

    private void EmitStloc(ushort index)
    {
        var variable = _variables[index];
        var value = _stackVariables.Pop();
        _builder.SetValueExpression(variable, value);
    }

    private string NewStackVariableName() => $"stack{_stackVariableCount++}";

    #endregion
}