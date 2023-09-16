using System.Text;

namespace CCG.Expressions;

public sealed class CDot : CExpression
{
    public readonly CExpression Expression;
    public readonly string Name;
    public readonly bool IsPointer;

    public CDot(CExpression expression, string name, bool isPointer = false)
    {
        Expression = expression;
        Name = name;
        IsPointer = isPointer;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append(Expression.ToString());
        if (IsPointer) builder.Append("->"); else builder.Append('.');
        builder.Append(Name);

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append(Expression.ToStringBeautified());
        if (IsPointer) builder.Append("->"); else builder.Append('.');
        builder.Append(Name);

        return builder.ToString();
    }
}