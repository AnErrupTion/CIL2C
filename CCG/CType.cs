namespace CCG;

public sealed class CType
{
    public static readonly CType Void = new("void");
    public static readonly CType Boolean = new("bool");
    public static readonly CType Int8 = new("int8_t");
    public static readonly CType Int16 = new("int16_t");
    public static readonly CType Int32 = new("int32_t");
    public static readonly CType Int64 = new("int64_t");
    public static readonly CType UInt8 = new("uint8_t");
    public static readonly CType UInt16 = new("uint16_t");
    public static readonly CType UInt32 = new("uint32_t");
    public static readonly CType UInt64 = new("uint64_t");
    public static readonly CType IntPtr = new("intptr_t");
    public static readonly CType UIntPtr = new("uintptr_t");
    public static readonly CType Size = new("size_t");
    public static readonly CType USize = new("usize_t");

    public readonly string Name;

    public CType(string name)
    {
        Name = name;
    }

    public static bool operator ==(CType type1, CType type2) => type1.Equals(type2);
    public static bool operator !=(CType type1, CType type2) => !type1.Equals(type2);

    public override int GetHashCode() => Name.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is not CType type) return false;
        return Name == type.Name;
    }

    public override string ToString() => Name;
}