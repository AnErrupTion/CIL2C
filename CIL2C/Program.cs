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
        var logger = new Logger(settings.Verbose);
        var minify = settings.Minify;
        var toggleComments = minify ? settings.ToggleComments : !settings.ToggleComments;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = settings.Threads == -1 ? (int)(Environment.ProcessorCount * 1.2) : settings.Threads
        };

        logger.Info($"Using {parallelOptions.MaxDegreeOfParallelism} threads");

        var stopwatch = Stopwatch.StartNew();

        logger.Info($"Loading input file as dnlib module: {settings.InputFile}");
        var module = ModuleDefMD.Load(settings.InputFile);

        logger.Info("Loading dnlib module into type system");
        var cilModule = DnlibLoader.LoadModuleDef(module);

        CBuilder builder = minify
            ? new CMinifiedBuilder(true, toggleComments)
            : new CBeautifiedBuilder(true, toggleComments);

        foreach (var include in settings.Includes) builder.AddInclude(include);
        foreach (var externalInclude in cilModule.ExternalIncludes) builder.AddInclude(externalInclude);

        // First, emit the types
        builder.AddComment("Types");

        Parallel.ForEach(cilModule.Types, parallelOptions, type =>
        {
            logger.Info($"Emitting type: {type.Key}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(type.Key);

            Emitter.EmitType(ref cBuilder, type.Key, type.Value);

            lock (builder) builder.Append(cBuilder);
        });

        // Then, emit the method definitions
        builder.AddComment("Method definitions");

        Parallel.ForEach(cilModule.AllMethods, parallelOptions, method =>
        {
            logger.Info($"Emitting method definition: {method.Key}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(method.Key);

            Emitter.EmitMethodDefinition(ref cBuilder, method.Value);

            lock (builder) builder.Append(cBuilder);
        });

        // After that, emit the fields
        builder.AddComment("Fields");

        Parallel.ForEach(cilModule.AllStaticNonEnumFields, parallelOptions, field =>
        {
            logger.Info($"Emitting field: {field.Key}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(field.Key);

            Emitter.EmitField(ref cBuilder, field.Value);

            lock (builder) builder.Append(cBuilder);
        });

        // Finally, emit the actual method bodies
        builder.AddComment("Methods");

        Parallel.ForEach(cilModule.AllBodiedMethods, parallelOptions, method =>
        {
            logger.Info($"Emitting method: {method.Key}.");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(method.Key);

            Emitter.EmitMethod(ref cilModule, ref cBuilder, method.Value);

            lock (builder) builder.Append(cBuilder);
        });

        logger.Info("Emitting main function");

        builder.AddComment("Entry point");
        Emitter.EmitMainFunction(ref builder, ref cilModule);

        stopwatch.Stop();
        logger.Info($"Took {stopwatch.Elapsed.Milliseconds} ms ({stopwatch.Elapsed.Seconds} s)");

        logger.Info("Saving file");
        File.WriteAllText(settings.OutputFile, builder.ToString());
    }
}