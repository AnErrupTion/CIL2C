using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace CIL2C;

public class Settings
{
    [Option('i', "input", Required = true, HelpText = "Sets the compiler input file.")]
    [NotNull]
    public string? InputFile { get; set; }

    [Option('o', "output", Required = true, HelpText = "Sets the compiler output file.")]
    [NotNull]
    public string? OutputFile { get; set; }

    [Option('m', "minify", Required = false, HelpText = "Minifies the C code.")]
    public bool Minify { get; set; }
}