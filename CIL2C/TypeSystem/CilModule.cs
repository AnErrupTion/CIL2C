namespace CIL2C.TypeSystem;

public sealed record CilModule(
    string FullName,
    string Name,
    CilMethod EntryPoint,
    List<string> ExternalIncludes,
    Dictionary<string, CilType> Types,
    Dictionary<string, CilField> AllStaticFields,
    Dictionary<string, CilField> AllStaticNonEnumFields,
    Dictionary<string, CilField> AllNonStaticFields,
    Dictionary<string, CilMethod> AllMethods,
    Dictionary<string, CilMethod> AllBodiedMethods,
    Dictionary<string, CilMethod> AllStaticConstructors
);