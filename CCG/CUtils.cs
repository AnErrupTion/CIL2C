namespace CCG;

public static class CUtils
{
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