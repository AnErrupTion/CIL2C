using CCG;

namespace CIL2C.TypeSystem;

public sealed record CilType(
    //CilModule ParentModule,
    string FullName,
    string Name,
    CType CType,
    bool IsEnum,
    bool IsClass,
    bool IsStruct,
    bool PackStruct,
    Dictionary<string, CilField> Fields
);