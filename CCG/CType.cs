using System.Text;

namespace CCG;

public sealed class CType
{
    public static readonly CType Void = new("void", false);
    public static readonly CType Boolean = new("bool", false);
    public static readonly CType Int8 = new("int8_t", false);
    public static readonly CType Int16 = new("int16_t", false);
    public static readonly CType Int32 = new("int32_t", false);
    public static readonly CType Int64 = new("int64_t", false);
    public static readonly CType UInt8 = new("uint8_t", false);
    public static readonly CType UInt16 = new("uint16_t", false);
    public static readonly CType UInt32 = new("uint32_t", false);
    public static readonly CType UInt64 = new("uint64_t", false);
    public static readonly CType IntPtr = new("intptr_t", false);
    public static readonly CType UIntPtr = new("uintptr_t", false);

    public readonly string Name;
    public readonly bool IsStruct;

    public CType(string name, bool isStruct)
    {
        Name = name;
        IsStruct = isStruct;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        if (IsStruct) builder.Append("struct ");
        builder.Append(Name);

        return builder.ToString();
    }
}