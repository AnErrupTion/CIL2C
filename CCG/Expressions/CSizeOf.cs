using System.Text;

namespace CCG.Expressions;

public sealed class CSizeOf : CExpression
{
    public readonly string Name;

    public CSizeOf(string name) => Name = name;

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("sizeof(");
        builder.Append(Name);
        builder.Append(')');

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append("sizeof(");
        builder.Append(Name);
        builder.Append(')');

        return builder.ToString();
    }
}