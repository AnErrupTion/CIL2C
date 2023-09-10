using System.Text.RegularExpressions;
using CCG;
using CCG.Expressions;
using dnlib.DotNet;

namespace CIL2C;

internal static partial class Utils
{
    public static readonly CConstantInt IntM1 = new(-1);
    public static readonly CConstantInt Int0 = new(0);
    public static readonly CConstantInt Int1 = new(1);
    public static readonly CConstantInt Int2 = new(2);
    public static readonly CConstantInt Int3 = new(3);
    public static readonly CConstantInt Int4 = new(4);
    public static readonly CConstantInt Int5 = new(5);
    public static readonly CConstantInt Int6 = new(6);
    public static readonly CConstantInt Int7 = new(7);
    public static readonly CConstantInt Int8 = new(8);

    public static readonly CConstantBool BoolTrue = new(true);
    public static readonly CConstantBool BoolFalse = new(false);

    public static CType GetCType(TypeSig type)
    {
        var name = type.FullName;

        if (name.EndsWith('*')) return CType.IntPtr;

        return name switch
        {
            "System.Void" => CType.Void,
            "System.Boolean" => CType.Boolean,
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
            _ => throw new ArgumentOutOfRangeException(nameof(type), type.FullName, null)
        };
    }

    public static CType GetAddFinalType(CType type1, CType type2)
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

    /*
     * A safe name is just a CIL method's full name with all non-alphanumeric characters replaced by underscores.
     */
    public static string GetSafeName(string name) => SafeNameRegex().Replace(name, "_");

    [GeneratedRegex("[^0-9a-zA-Z]+")]
    private static partial Regex SafeNameRegex();
}