using System.Text;

namespace CCG.Expressions;

public sealed class CBinaryOperation : CExpression
{
    public readonly CExpression Left;
    public readonly CBinaryOperator Operator;
    public readonly CExpression Right;

    public CBinaryOperation(CExpression left, CBinaryOperator op, CExpression right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append('(');
        builder.Append(Left.ToString());
        builder.Append(CUtils.GetBinaryOperator(Operator));
        builder.Append(Right.ToString());
        builder.Append(')');

        return builder.ToString();
    }

    public override string ToStringBeautified()
    {
        var builder = new StringBuilder();

        builder.Append('(');
        builder.Append(Left.ToStringBeautified());
        builder.Append(' ');
        builder.Append(CUtils.GetBinaryOperator(Operator));
        builder.Append(' ');
        builder.Append(Right.ToStringBeautified());
        builder.Append(')');

        return builder.ToString();
    }
}