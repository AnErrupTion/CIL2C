using System.Text;

namespace CCG.Expressions;

public sealed class CPointer : CExpression
{
    public readonly CExpression Value;

    public CPointer(CExpression value) => Value = value;

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("*(");
        builder.Append(Value.ToString());
        builder.Append(')');

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append("*(");
        builder.Append(Value.ToStringBeautified());
        builder.Append(')');

        return builder.ToString();
    }
}