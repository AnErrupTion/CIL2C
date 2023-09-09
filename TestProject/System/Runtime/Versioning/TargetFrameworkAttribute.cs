namespace System.Runtime.Versioning;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TargetFrameworkAttribute : Attribute
{
    public string FrameworkName { get; }

    public string? FrameworkDisplayName { get; set; }

    public TargetFrameworkAttribute(string frameworkName)
    {
        //if (frameworkName == null) throw new ArgumentNullException(frameworkName);
        
        FrameworkName = frameworkName;
    }
}