using CCG.Expressions;

namespace CIL2C.TypeSystem;

public sealed record CilField(
    CilType ParentType,
    CilType Type,
    string FullName,
    string Name,
    bool IsStatic,
    CVariable Definition,

    // Not translated from dnlib
    object? ConstantValue
);