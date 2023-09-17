using System.Text.RegularExpressions;
using CCG;
using CCG.Expressions;

namespace CIL2C;

internal static partial class Utils
{
    public static readonly CType Void = new(GetSafeName("System.Void"));
    public static readonly CType Boolean = new(GetSafeName("System.Boolean"));
    public static readonly CType Char = new(GetSafeName("System.Char"));
    public static readonly CType SByte = new(GetSafeName("System.SByte"));
    public static readonly CType Int16 = new(GetSafeName("System.Int16"));
    public static readonly CType Int32 = new(GetSafeName("System.Int32"));
    public static readonly CType Int64 = new(GetSafeName("System.Int64"));
    public static readonly CType Byte = new(GetSafeName("System.Byte"));
    public static readonly CType UInt16 = new(GetSafeName("System.UInt16"));
    public static readonly CType UInt32 = new(GetSafeName("System.UInt32"));
    public static readonly CType UInt64 = new(GetSafeName("System.UInt64"));
    public static readonly CType IntPtr = new(GetSafeName("System.IntPtr"));
    public static readonly CType UIntPtr = new(GetSafeName("System.UIntPtr"));

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

    // A safe name is just a CIL method's full name with all non-alphanumeric characters replaced by underscores.
    public static string GetSafeName(string name) => SafeNameRegex().Replace(name, "_");

    [GeneratedRegex("[^0-9a-zA-Z]+")]
    private static partial Regex SafeNameRegex();
}