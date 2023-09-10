using System.Text.RegularExpressions;
using CCG;
using CCG.Expressions;
using dnlib.DotNet;

namespace CIL2C;

internal static partial class Utils
{
    public static readonly CType Object = new(GetSafeName("System.Object"), true);
    public static readonly CType Void = new(GetSafeName("System.Void"), true);
    public static readonly CType Boolean = new(GetSafeName("System.Boolean"), true);
    public static readonly CType SByte = new(GetSafeName("System.SByte"), true);
    public static readonly CType Int16 = new(GetSafeName("System.Int16"), true);
    public static readonly CType Int32 = new(GetSafeName("System.Int32"), true);
    public static readonly CType Int64 = new(GetSafeName("System.Int64"), true);
    public static readonly CType Byte = new(GetSafeName("System.Byte"), true);
    public static readonly CType UInt16 = new(GetSafeName("System.UInt16"), true);
    public static readonly CType UInt32 = new(GetSafeName("System.UInt32"), true);
    public static readonly CType UInt64 = new(GetSafeName("System.UInt64"), true);
    public static readonly CType IntPtr = new(GetSafeName("System.IntPtr"), true);
    public static readonly CType UIntPtr = new(GetSafeName("System.UIntPtr"), true);

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

    public static readonly Dictionary<string, CType> Types = new();
    public static readonly Dictionary<string, CVariable> Fields = new();

    public static CType GetCType(TypeSig type)
    {
        var name = type.FullName;

        if (name.EndsWith('*')) return IntPtr;
        if (name.EndsWith("[]")) return UIntPtr;
        if (Types.TryGetValue(name, out var value)) return value;

        throw new ArgumentOutOfRangeException(nameof(name), name, null);
    }

    public static CType GetBinaryNumericOperationType(CType type1, CType type2)
    {
        if (type1 == Int32 && type2 == Int32) return Int32;
        if (type1 == Int32 && type2 == IntPtr) return IntPtr;
        if (type1 == Int64 && type2 == Int64) return Int64;
        if ((type1 == IntPtr && type2 == Int32) || (type1 == IntPtr && type2 == IntPtr)) return IntPtr;
        return Int32;
    }

    // A safe name is just a CIL method's full name with all non-alphanumeric characters replaced by underscores.
    public static string GetSafeName(string name) => SafeNameRegex().Replace(name, "_");

    [GeneratedRegex("[^0-9a-zA-Z]+")]
    private static partial Regex SafeNameRegex();
}