namespace CIL2C.TypeSystem;

public sealed record CilMethod(
    CilType ParentType,
    CilType ReturnType,
    string FullName,
    string Name,
    bool IsStaticConstructor,
    List<CilMethodArgument> Arguments,
    CilMethodBody Body
);