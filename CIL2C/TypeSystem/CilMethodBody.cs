using dnlib.DotNet.Emit;

namespace CIL2C.TypeSystem;

public sealed record CilMethodBody(
    ushort MaxStackSize,
    bool InitializeLocals,
    List<CilLocal> Locals,

    // Not translated from dnlib
    IList<Instruction> Instructions
);