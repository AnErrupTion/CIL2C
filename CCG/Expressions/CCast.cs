using System.Text;

namespace CCG.Expressions;

public sealed class CCast : CExpression
{
    public readonly bool IsConst;
    public readonly bool IsPointer;
    public readonly CType Type;
    public readonly CExpression Value;

    public CCast(bool isConst, bool isPointer, CType type, CExpression value)
    {
        IsConst = isConst;
        IsPointer = isPointer;
        Type = type;
        Value = value;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append('(');
        if (IsConst) builder.Append("const ");
        builder.Append(CUtils.GetType(Type));
        if (IsPointer) builder.Append('*');
        builder.Append(')');
        builder.Append(Value.ToString());
        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();
        builder.Append('(');
        if (IsConst) builder.Append("const ");
        builder.Append(CUtils.GetType(Type));
        if (IsPointer) builder.Append(" *");
        builder.Append(") ");
        builder.Append(Value.ToStringBeautified());
        return builder.ToString();
    }
}