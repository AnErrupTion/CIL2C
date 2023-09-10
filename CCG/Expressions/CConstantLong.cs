namespace CCG.Expressions;

public sealed class CConstantLong : CExpression
{
    private readonly long _value;

    public CConstantLong(long value) => _value = value;

    public override string ToString() => _value.ToString();

    public override string ToStringBeautified() => _value.ToString();
}