namespace CIL2C.TypeSystem;

public sealed record CilModule(
    string FullName,
    string Name,
    CilMethod EntryPoint,
    Dictionary<string, CilType> Types,
    Dictionary<string, CilField> AllStaticFields,
    Dictionary<string, CilField> AllStaticNonEnumFields,
    Dictionary<string, CilField> AllNonStaticFields,
    Dictionary<string, CilMethod> AllMethods,
    Dictionary<string, CilMethod> AllStaticConstructors
);