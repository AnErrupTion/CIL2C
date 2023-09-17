namespace CIL2C.TypeSystem;

public sealed record CilMethodArgument(
    //CilMethod ParentMethod,
    CilType Type,
    string Name
);