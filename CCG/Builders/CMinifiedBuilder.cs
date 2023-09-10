using System.Text;
using CCG.Expressions;

namespace CCG.Builders;

public class CMinifiedBuilder : CBuilder
{
    private readonly StringBuilder _builder = new();

    public override string ToString() => _builder.ToString();

    public CMinifiedBuilder(bool enableComments = false) : base(enableComments)
    { }

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

    public override void BeginBlock() => _builder.Append('{');

    public override void EndBlock() => _builder.Append("};");

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
        _builder.Append(variable.Type);
        if (variable.IsPointer) _builder.Append('*');
        _builder.Append(' ');
        _builder.Append(variable.ToString());
        _builder.Append(';');
    }

    public override void AddVariable(CVariable variable, CExpression value)
    {
        if (variable.IsConst) _builder.Append("const ");
        _builder.Append(variable.Type);
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

    #region Structs

    public override void AddStruct(string name)
    {
        _builder.Append("struct ");
        _builder.Append(name);
    }

    #endregion

    #region Enums

    public override void AddEnum(string name, params CEnumField[] values)
    {
        _builder.Append("enum ");
        _builder.Append(name);

        BeginBlock();
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            _builder.Append(value.Name);

            if (value.Value != null)
            {
                _builder.Append('=');
                _builder.Append(value.Value.ToString());
            }

            if (i != values.Length - 1) _builder.Append(',');
        }
        EndBlock();
    }

    #endregion

    #region Functions

    public override void AddFunction(CType returnType, string name, bool isPrototype, params CVariable[] args)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Function name is null or empty.", nameof(name));
        if (name.Contains(' ')) throw new ArgumentException("Function name contains at least one space.", nameof(name));

        _builder.Append(returnType);
        _builder.Append(' ');
        _builder.Append(name);
        _builder.Append('(');

        if (args.Length > 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.IsConst) _builder.Append("const ");
                _builder.Append(arg.Type);
                if (arg.IsPointer) _builder.Append('*');
                _builder.Append(' ');
                _builder.Append(arg.Name);

                if (i == args.Length - 1) continue;

                _builder.Append(',');
            }
        } else _builder.Append("void");

        if (isPrototype) _builder.Append(");"); else _builder.Append(')');
    }

    #endregion

    #region Calls

    public override void AddCall(CCall call)
    {
        _builder.Append(call.ToString());
        _builder.Append(';');
    }

    #endregion

    #region Conditions

    public override void AddIf(CCompareOperation operation)
    {
        _builder.Append("if(");
        _builder.Append(operation.ToStringBeautified());
        _builder.Append(')');
        _builder.AppendLine();
    }

    public override void AddElse() => _builder.AppendLine("else");

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