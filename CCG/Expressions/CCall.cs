using System.Text;

namespace CCG.Expressions;

public sealed class CCall : CExpression
{
    public readonly string FunctionName;
    public readonly CExpression[] Arguments;

    public CCall(string functionName, params CExpression[] arguments)
    {
        FunctionName = functionName;
        Arguments = arguments;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(FunctionName);
        builder.Append('(');

        for (var i = 0; i < Arguments.Length; i++)
        {
            var argument = Arguments[i];
            builder.Append(argument.ToString());
            if (i != Arguments.Length - 1) builder.Append(',');
        }

        builder.Append(')');
        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();
        builder.Append(FunctionName);
        builder.Append('(');

        for (var i = 0; i < Arguments.Length; i++)
        {
            var argument = Arguments[i];
            builder.Append(argument.ToStringBeautified());
            if (i != Arguments.Length - 1) builder.Append(", ");
        }

        builder.Append(')');
        return builder.ToString();
    }
}