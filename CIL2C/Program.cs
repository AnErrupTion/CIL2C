using System.Diagnostics;
using CCG;
using CCG.Builders;
using CommandLine;
using dnlib.DotNet;

namespace CIL2C;

public static class Program
{
    public static void Main(string[] args)
    {
        var settings = Parser.Default.ParseArguments<Settings>(args).Value;
        var minify = settings.Minify;
        var toggleComments = minify ? settings.ToggleComments : !settings.ToggleComments;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = settings.Threads == -1 ? (int)(Environment.ProcessorCount * 1.2) : settings.Threads
        };

        Logger.Initialize(settings.Verbose);

        Logger.Info($"Using {parallelOptions.MaxDegreeOfParallelism} threads");
        var stopwatch = Stopwatch.StartNew();

        Logger.Info($"Loading input file as dnlib module: {settings.InputFile}");
        var module = ModuleDefMD.Load(settings.InputFile);

        Logger.Info("Loading dnlib module into type system");
        var cilModule = DnlibLoader.LoadModuleDef(module);

        CBuilder builder = minify
            ? new CMinifiedBuilder(true, toggleComments)
            : new CBeautifiedBuilder(true, toggleComments);

        foreach (var include in settings.Includes) builder.AddInclude(include);
        foreach (var externalInclude in cilModule.ExternalIncludes) builder.AddInclude(externalInclude);

        // First, emit the types
        builder.AddComment("Types");

        var emittedStructs = new List<string>();
        foreach (var type in cilModule.Types)
        {
            Logger.VerboseInfo($"Emitting type: {type.Key}");

            var cBuilder = builder.Clone();
            Emitter.EmitType(ref cBuilder, ref emittedStructs, type.Value);

            builder.Append(cBuilder);
        }

        // Then, emit the method definitions
        builder.AddComment("Method definitions");

        Parallel.ForEach(cilModule.AllMethods, parallelOptions, method =>
        {
            Logger.VerboseInfo($"Emitting method definition: {method.Key}");

            var cBuilder = builder.Clone();
            Emitter.EmitMethodDefinition(ref cBuilder, method.Value);

            lock (builder) builder.Append(cBuilder);
        });

        // After that, emit the fields
        builder.AddComment("Fields");

        Parallel.ForEach(cilModule.AllStaticNonEnumFields, parallelOptions, field =>
        {
            Logger.VerboseInfo($"Emitting field: {field.Key}");

            var cBuilder = builder.Clone();
            Emitter.EmitField(ref cBuilder, field.Value);

            lock (builder) builder.Append(cBuilder);
        });

        // Finally, emit the actual method bodies
        builder.AddComment("Methods");

        Parallel.ForEach(cilModule.AllBodiedMethods, parallelOptions, method =>
        {
            Logger.VerboseInfo($"Emitting method: {method.Key}.");

            var cBuilder = builder.Clone();
            Emitter.EmitMethod(ref cilModule, ref cBuilder, method.Value);

            lock (builder) builder.Append(cBuilder);
        });

        Logger.VerboseInfo("Emitting main function");

        builder.AddComment("Entry point");
        Emitter.EmitMainFunction(ref builder, ref cilModule);

        stopwatch.Stop();
        Logger.Info($"Took {stopwatch.Elapsed.Milliseconds} ms ({stopwatch.Elapsed.Seconds} s)");

        Logger.Info("Saving file");
        File.WriteAllText(settings.OutputFile, builder.ToString());
    }
}