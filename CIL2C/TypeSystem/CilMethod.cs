namespace CIL2C.TypeSystem;

public sealed record CilMethod(
    CilType ParentType,
    CilType ReturnType,
    string FullName,
    string Name,
    bool NeedsExternalCFunction,
    string? ExternalCFunctionName,
    List<CilMethodArgument> Arguments,
    CilMethodBody? Body
);