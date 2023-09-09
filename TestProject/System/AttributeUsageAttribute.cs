namespace System;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AttributeUsageAttribute : Attribute
{
    public AttributeTargets ValidOn { get; }

    public bool AllowMultiple { get; set; }

    public bool Inherited { get; set; }

    public AttributeUsageAttribute(AttributeTargets validOn)
    {
        ValidOn = validOn;
        Inherited = true;
    }

    public AttributeUsageAttribute(AttributeTargets validOn, bool allowMultiple, bool inherited)
    {
        ValidOn = validOn;
        AllowMultiple = allowMultiple;
        Inherited = inherited;
    }
}