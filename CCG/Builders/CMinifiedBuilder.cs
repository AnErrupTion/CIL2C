using System.Text;
using CCG.Expressions;

namespace CCG.Builders;

public class CMinifiedBuilder : CBuilder
{
    private readonly StringBuilder _builder = new();

    public override string ToString() => _builder.ToString();

    public CMinifiedBuilder(bool enableComments = false) : base(enableComments)
        => _builder.AppendLine("#include <stdint.h>");

    #region Comments

    protected override void InternalAddComment(string text)
    {
        _builder.Append("/*");
        _builder.Append(text);
        _builder.Append("*/");
    }

    #endregion

    #region Includes

    public override void AddInclude(string file)
    {
        _builder.Append("#include \"");
        _builder.Append(file);
        _builder.Append('"');
        _builder.AppendLine();
    }

    #endregion

    #region Blocks

    public override void BeginBlock()
    {
        _builder.Append('{');
    }

    public override void EndBlock(bool requireNewLine)
    {
        _builder.Append('}');
        if (requireNewLine) _builder.AppendLine();
    }

    #endregion

    #region Labels

    public override void AddLabel(string label)
    {
        _builder.Append(label);
        _builder.Append(":;");
    }

    public override void GoToLabel(string label)
    {
        _builder.Append("goto ");
        _builder.Append(label);
        _builder.Append(';');
    }

    #endregion

    #region Variables

    public override void AddVariable(CVariable variable)
    {
        if (variable.IsConst) _builder.Append("const ");
        _builder.Append(CUtils.GetType(variable.Type));
        if (variable.IsPointer) _builder.Append('*');
        _builder.Append(' ');
        _builder.Append(variable.ToString());
        _builder.Append(';');
    }

    public override void AddVariable(CVariable variable, CExpression value)
    {
        if (variable.IsConst) _builder.Append("const ");
        _builder.Append(CUtils.GetType(variable.Type));
        if (variable.IsPointer) _builder.Append('*');
        _builder.Append(' ');
        _builder.Append(variable.ToString());
        _builder.Append('=');
        _builder.Append(value.ToString());
        _builder.Append(';');
    }

    #endregion

    #region Value Expressions

    public override void SetValueExpression(CExpression expression, CExpression value)
    {
        _builder.Append(expression.ToString());
        _builder.Append('=');
        _builder.Append(value.ToString());
        _builder.Append(';');
    }

    #endregion

    #region Functions

    public override void AddFunction(CType returnType, string name, params CVariable[] args)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Function name is null or empty.", nameof(name));
        if (name.Contains(' ')) throw new ArgumentException("Function name contains at least one space.", nameof(name));

        _builder.Append(CUtils.GetType(returnType));
        _builder.Append(' ');
        _builder.Append(name);
        _builder.Append('(');

        if (args.Length > 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.IsConst) _builder.Append("const ");
                _builder.Append(CUtils.GetType(arg.Type));
                if (arg.IsPointer) _builder.Append('*');
                _builder.Append(' ');
                _builder.Append(arg.Name);

                if (i == args.Length - 1) continue;

                _builder.Append(',');
            }
        } else _builder.Append("void");

        _builder.Append(')');
    }

    #endregion

    #region Calls

    public override void AddCall(CCall call)
    {
        _builder.Append(call.ToString());
        _builder.Append(';');
    }

    #endregion

    #region Returns

    public override void AddReturn()
    {
        _builder.Append("return;");
    }

    public override void AddReturn(CExpression expression)
    {
        _builder.Append("return ");
        _builder.Append(expression.ToString());
        _builder.Append(';');
    }

    #endregion
}