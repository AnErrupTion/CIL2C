namespace System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public class StructLayoutAttribute : Attribute
{
    public LayoutKind Value { get; }

    public int Pack;
    public int Size;
    public CharSet CharSet;

    public StructLayoutAttribute(LayoutKind layoutKind)
    {
        Value = layoutKind;
    }

    public StructLayoutAttribute(short layoutKind)
    {
        Value = (LayoutKind)layoutKind;
    }
}