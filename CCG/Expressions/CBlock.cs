using System.Text;

namespace CCG.Expressions;

public sealed class CBlock : CExpression
{
    public readonly CExpression? Expression;

    public CBlock(CExpression? expression) => Expression = expression;

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append('{');
        builder.Append(Expression?.ToString());
        builder.Append('}');

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append("{ ");
        builder.Append(Expression?.ToStringBeautified());
        builder.Append(" }");

        return builder.ToString();
    }
}