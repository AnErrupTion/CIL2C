using CCG.Expressions;

namespace CIL2C.TypeSystem;

public sealed record CilLocal(
    CilType Type,
    CVariable Definition
);