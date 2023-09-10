namespace CCG;

public static class CUtils
{
    public static string GetType(CType type) => type switch
    {
        CType.Void => "void",
        CType.Boolean => "bool",
        CType.Int8 => "int8_t",
        CType.Int16 => "int16_t",
        CType.Int32 => "int32_t",
        CType.Int64 => "int64_t",
        CType.UInt8 => "uint8_t",
        CType.UInt16 => "uint16_t",
        CType.UInt32 => "uint32_t",
        CType.UInt64 => "uint64_t",
        CType.IntPtr => "intptr_t",
        CType.UIntPtr => "uintptr_t",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static char GetBinaryOperator(CBinaryOperator op) => op switch
    {
        CBinaryOperator.Add => '+',
        CBinaryOperator.Sub => '-',
        CBinaryOperator.Mul => '*',
        CBinaryOperator.Div => '/',
        CBinaryOperator.Mod => '%',
        CBinaryOperator.And => '&',
        CBinaryOperator.Or => '|',
        CBinaryOperator.Xor => '^',
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    public static string GetCompareOperator(CCompareOperator op) => op switch
    {
        CCompareOperator.Equal => "==",
        CCompareOperator.NotEqual => "!=",
        CCompareOperator.BelowOrEqual => "<=",
        CCompareOperator.AboveOrEqual => ">=",
        CCompareOperator.Below => "<",
        CCompareOperator.Above => ">",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };
}