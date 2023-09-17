using System;

namespace TestProject;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CFunctionImportAttribute : Attribute
{
    public string? IncludeFile;
    public string? FunctionName;
}