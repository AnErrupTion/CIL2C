using System.Text;
using CCG.Expressions;

namespace CCG.Builders;

public class CBeautifiedBuilder : CBuilder
{
    private readonly StringBuilder _builder = new();

    private uint _tabs;

    public CBeautifiedBuilder(bool addRequiredIncludes, bool enableComments = true) : base(addRequiredIncludes, enableComments)
    {
        if (!addRequiredIncludes) return;

        _builder.AppendLine("#include <stddef.h>");
        _builder.AppendLine("#include <stdint.h>");
        _builder.AppendLine("#include <stdbool.h>");
    }

    public override string ToString() => _builder.ToString();

    public override CBuilder Clone() => new CBeautifiedBuilder(false, EnableComments);

    public override void Append(CBuilder builder) => _builder.Append(builder);

    #region Comments

    protected override void InternalAddComment(string text)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("// ");
        _builder.AppendLine(text);
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
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append('{');
        _builder.AppendLine();
        _tabs++;
    }

    public override void EndBlock()
    {
        _tabs--;
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.AppendLine("};");
    }

    #endregion

    #region Labels

    public override void AddLabel(string label)
    {
        // Adding a label is the only case where we don't apply our tabs, for style
        _builder.Append(label);
        _builder.Append(": ;");
        _builder.AppendLine();
    }

    public override void GoToLabel(string label)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("goto ");
        _builder.Append(label);
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Variables

    public override void AddVariable(CVariable variable)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        if (variable.IsConst) _builder.Append("const ");
        _builder.Append(variable.Type);
        if (variable.IsPointer) _builder.Append(" *");
        _builder.Append(' ');
        _builder.Append(variable.ToStringBeautified());
        _builder.Append(';');
        _builder.AppendLine();
    }

    public override void AddVariable(CVariable variable, CExpression value)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        if (variable.IsConst) _builder.Append("const ");
        _builder.Append(variable.Type);
        if (variable.IsPointer) _builder.Append(" *");
        _builder.Append(' ');
        _builder.Append(variable.ToStringBeautified());
        _builder.Append(" = ");
        _builder.Append(value.ToStringBeautified());
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Value Expressions

    public override void SetValueExpression(CExpression expression, CExpression value)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append(expression.ToStringBeautified());
        _builder.Append(" = ");
        _builder.Append(value.ToStringBeautified());
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Structs

    public override void AddStruct(string name, bool pack, params CStructField[] fields)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("typedef struct ");
        _builder.AppendLine(name);
        _builder.Append('{');
        _builder.AppendLine();
        _tabs++;

        foreach (var field in fields)
        {
            for (var i = 0; i < _tabs; i++) _builder.Append('\t');

            _builder.Append(field.Type);
            if (field.IsPointer) _builder.Append(" *");
            _builder.Append(' ');
            _builder.Append(field.Name);
            _builder.Append(';');
            _builder.AppendLine();
        }

        _tabs--;
        _builder.Append("} ");

        if (pack) _builder.Append("__attribute__((__packed__)) ");

        _builder.Append(name);
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Enums

    public override void AddEnum(string name, params CEnumField[] fields)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("typedef enum ");
        _builder.AppendLine(name);
        _builder.Append('{');
        _builder.AppendLine();
        _tabs++;

        for (var i = 0; i < fields.Length; i++)
        {
            for (var j = 0; j < _tabs; j++) _builder.Append('\t');

            var field = fields[i];
            _builder.Append(field.Name);

            if (field.Value != null)
            {
                _builder.Append(" = ");
                _builder.Append(field.Value.ToStringBeautified());
            }

            if (i != fields.Length - 1) _builder.Append(',');

            _builder.AppendLine();
        }

        _tabs--;
        _builder.Append("} ");
        _builder.Append(name);
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Functions

    public override void AddFunction(CType returnType, string name, bool isPrototype, params CVariable[] args)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Function name is null or empty.", nameof(name));
        if (name.Contains(' ')) throw new ArgumentException("Function name contains at least one space.", nameof(name));

        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
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
                if (arg.IsPointer) _builder.Append(" *");
                _builder.Append(' ');
                _builder.Append(arg.Name);

                if (i == args.Length - 1) continue;

                _builder.Append(", ");
            }
        } else _builder.Append("void");

        if (isPrototype) _builder.Append(");"); else _builder.Append(')');
        _builder.AppendLine();
    }

    #endregion

    #region Calls

    public override void AddCall(CCall call)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append(call.ToStringBeautified());
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion

    #region Conditions

    public override void AddIf(CCompareOperation operation)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("if (");
        _builder.Append(operation.ToStringBeautified());
        _builder.Append(')');
        _builder.AppendLine();
    }

    public override void AddElse()
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.AppendLine("else");
    }

    #endregion

    #region Returns

    public override void AddReturn()
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.AppendLine("return;");
    }

    public override void AddReturn(CExpression expression)
    {
        for (var i = 0; i < _tabs; i++) _builder.Append('\t');
        _builder.Append("return ");
        _builder.Append(expression.ToStringBeautified());
        _builder.Append(';');
        _builder.AppendLine();
    }

    #endregion
}