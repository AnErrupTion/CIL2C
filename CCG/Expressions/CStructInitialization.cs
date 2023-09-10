using System.Text;

namespace CCG.Expressions;

public sealed class CStructInitialization : CExpression
{
    public readonly Dictionary<string, CExpression> Values;

    public CStructInitialization(Dictionary<string, CExpression> values) => Values = values;

    public override string ToString()
    {
        var builder = new StringBuilder();
        var index = 0U;

        foreach (var value in Values)
        {
            builder.Append('.');
            builder.Append(value.Key);
            builder.Append('=');
            builder.Append(value.Value.ToString());
            if (index != Values.Count - 1) builder.Append(',');

            index++;
        }

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();
        var index = 0U;

        foreach (var value in Values)
        {
            builder.Append('.');
            builder.Append(value.Key);
            builder.Append(" = ");
            builder.Append(value.Value.ToStringBeautified());
            if (index != Values.Count - 1) builder.Append(", ");

            index++;
        }

        return builder.ToString();
    }
}