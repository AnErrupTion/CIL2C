namespace CCG.Expressions;

public sealed class CConstantInt : CExpression
{
    private readonly int _value;

    public CConstantInt(int value) => _value = value;

    public override string ToString() => _value.ToString();

    public override string ToStringBeautified() => _value.ToString();
}