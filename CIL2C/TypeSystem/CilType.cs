using CCG;
using dnlib.DotNet;

namespace CIL2C.TypeSystem;

public sealed record CilType(
    //CilModule ParentModule,
    string FullName,
    string Name,
    CType CType,
    bool IsEnum,
    bool IsClass,
    bool IsStruct,
    Dictionary<string, CilField> Fields,

    // Not translated from dnlib
    CustomAttributeCollection CustomAttributes
);