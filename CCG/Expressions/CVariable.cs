namespace CCG.Expressions;

public sealed class CVariable : CExpression
{
    public readonly bool IsConst;
    public readonly bool IsPointer;
    public readonly CType Type;
    public readonly string Name;

    public CVariable(bool isConst, bool isPointer, CType type, string name)
    {
        IsConst = isConst;
        IsPointer = isPointer;
        Type = type;
        Name = name;
    }

    public override string ToString() => Name;

    public override string ToStringBeautified() => Name;
}