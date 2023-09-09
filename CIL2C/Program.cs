using System.Text.RegularExpressions;
using CCG;
using CCG.Builders;
using CCG.Expressions;
using CommandLine;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CIL2C;

public static partial class Program
{
    private static readonly CConstantInt IntZero = new(0);
    private static readonly CConstantInt IntOne = new(1);

    private static CBuilder _builder = null!;

    public static void Main(string[] args)
    {
        var settings = Parser.Default.ParseArguments<Settings>(args).Value;
        _builder = settings.Minify ? new CMinifiedBuilder() : new CBeautifiedBuilder();

        var module = ModuleDefMD.Load(settings.InputFile);

        Emit(module.EntryPoint);

        _builder.AddFunction(CType.Int32, "main");
        _builder.BeginBlock();
        _builder.AddCall(new CCall(GetSafeName(module.EntryPoint.FullName)));
        _builder.AddReturn(IntZero);
        _builder.EndBlock(true);

        var code = _builder.ToString();

        File.WriteAllText(settings.OutputFile, code);
    }

    private static void Emit(MethodDef method)
    {
        _builder.AddFunction(GetCType(method.ReturnType), GetSafeName(method.FullName));
        _builder.BeginBlock();

        var variables = new List<CVariable>(method.Body.Variables.Count);
        var stackVariables = new Stack<CVariable>();
        var stackVariableCount = 0UL;

        _builder.AddComment("Locals");
        foreach (var local in method.Body.Variables)
        {
            var variable = new CVariable(false, false, GetCType(local.Type), local.Name);
            variables.Add(variable);
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
                    var value = stackVariables.Peek();
                    stackVariables.Push(value);
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
                    var value2 = stackVariables.Pop();
                    var value1 = stackVariables.Pop();
                    var type = GetAddFinalType(value1.Type, value2.Type);
                    var variable = new CVariable(true, false, type, $"stack{stackVariableCount++}");
                    var result = new CCast(true, false, type, new CBinaryOperation(value1, CBinaryOperator.Add, value2));
                    _builder.AddVariable(variable, result);
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Ldc_I4_1:
                {
                    var variable = new CVariable(true, false, CType.Int32, $"stack{stackVariableCount++}");
                    _builder.AddVariable(variable, IntOne);
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Ldc_I4_S:
                case Code.Ldc_I4:
                {
                    var variable = new CVariable(true, false, CType.Int32, $"stack{stackVariableCount++}");
                    _builder.AddVariable(variable, new CConstantInt(Convert.ToInt32(instruction.Operand)));
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Conv_I:
                {
                    var value = stackVariables.Pop();
                    var variable = new CVariable(true, false, CType.IntPtr, $"stack{stackVariableCount++}");
                    _builder.AddVariable(variable, new CCast(true, false, variable.Type, value));
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Ldloc_0:
                {
                    var localVariable = variables[0];
                    var variable = new CVariable(true, localVariable.IsPointer, localVariable.Type, $"stack{stackVariableCount++}");
                    _builder.AddVariable(variable, localVariable);
                    stackVariables.Push(variable);
                    break;
                }
                case Code.Stloc_0:
                {
                    var variable = variables[0];
                    var value = stackVariables.Pop();
                    _builder.SetValueExpression(variable, value);
                    break;
                }
                case Code.Stind_I1:
                {
                    var value = stackVariables.Pop();
                    var address = stackVariables.Pop();
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
    
    private static CType GetCType(TypeSig type) => type.FullName switch
    {
        "System.Void" => CType.Void,
        "System.SByte" => CType.Int8,
        "System.Int16" => CType.Int16,
        "System.Int32" => CType.Int32,
        "System.Int64" => CType.Int64,
        "System.Byte" => CType.UInt8,
        "System.UInt16" => CType.UInt16,
        "System.UInt32" => CType.UInt32,
        "System.UInt64" => CType.UInt64,
        "System.IntPtr" => CType.IntPtr,
        "System.UIntPtr" => CType.UIntPtr,
        "System.Void*" => CType.IntPtr,
        "System.SByte*" => CType.IntPtr,
        "System.Int16*" => CType.IntPtr,
        "System.Int32*" => CType.IntPtr,
        "System.Int64*" => CType.IntPtr,
        "System.Byte*" => CType.IntPtr,
        "System.UInt16*" => CType.IntPtr,
        "System.UInt32*" => CType.IntPtr,
        "System.UInt64*" => CType.IntPtr,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type.FullName, null)
    };

    private static CType GetAddFinalType(CType type1, CType type2)
    {
        CType type;

        switch (type1)
        {
            case CType.Int32 when type2 == CType.Int32:
            {
                type = CType.Int32;
                break;
            }
            case CType.Int32 when type2 == CType.IntPtr:
            {
                type = CType.IntPtr;
                break;
            }
            case CType.Int64 when type2 == CType.Int64:
            {
                type = CType.Int64;
                break;
            }
            case CType.IntPtr when type2 == CType.Int32:
            case CType.IntPtr when type2 == CType.IntPtr:
            {
                type = CType.IntPtr;
                break;
            }
            default:
            {
                type = CType.Int32;
                break;
            }
        }

        return type;
    }
    
    private static string GetSafeName(string name) => SafeNameRegex().Replace(name, "_");

    [GeneratedRegex("[^0-9a-zA-Z]+")]
    private static partial Regex SafeNameRegex();
}