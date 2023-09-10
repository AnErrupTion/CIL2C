using System.Text;

namespace CCG.Expressions;

public sealed class CDot : CExpression
{
    public readonly CExpression Expression;
    public readonly string Name;

    public CDot(CExpression expression, string name)
    {
        Expression = expression;
        Name = name;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append(Expression.ToString());
        builder.Append('.');
        builder.Append(Name);

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append(Expression.ToStringBeautified());
        builder.Append('.');
        builder.Append(Name);

        return builder.ToString();
    }
}