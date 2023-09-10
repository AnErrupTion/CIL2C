namespace CCG.Expressions;

public sealed class CConstantBool : CExpression
{
    private readonly bool _value;

    public CConstantBool(bool value) => _value = value;

    public override string ToString() => _value ? "true" : "false";

    public override string ToStringBeautified() => _value ? "true" : "false";
}