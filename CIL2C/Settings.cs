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

    [Option('c', "comments", Required = false, HelpText = "Toggles comments in the C code (disable if beautifying, else enable).")]
    public bool ToggleComments { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Enables verbose output.")]
    public bool Verbose { get; set; }

    [Option('t', "threads", Required = false, HelpText = "Changes the number of threads. Set to -1 for auto (default).")]
    public int Threads { get; set; } = -1;
}