using CCG.Expressions;

namespace CCG;

public abstract class CBuilder
{
    public bool EnableComments { get; set; }

    protected CBuilder(bool enableComments) => EnableComments = enableComments;

    public void AddComment(string text)
    {
        if (EnableComments) InternalAddComment(text);
    }

    protected abstract void InternalAddComment(string text);

    public abstract void AddInclude(string file);

    public abstract void BeginBlock();
    public abstract void EndBlock();

    public abstract void AddLabel(string label);
    public abstract void GoToLabel(string label);

    public abstract void AddVariable(CVariable variable);
    public abstract void AddVariable(CVariable variable, CExpression value);

    public abstract void SetValueExpression(CExpression expression, CExpression value);

    public abstract void AddStruct(string name);

    public abstract void AddFunction(CType returnType, string name, bool isPrototype, params CVariable[] args);

    public abstract void AddCall(CCall call);

    public abstract void AddIf(CCompareOperation operation);
    public abstract void AddElse();

    public abstract void AddReturn();
    public abstract void AddReturn(CExpression expression);
}