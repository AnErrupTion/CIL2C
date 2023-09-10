﻿using System.Collections.Concurrent;
using System.Diagnostics;
using CCG;
using CCG.Builders;
using CCG.Expressions;
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

        if (settings.Verbose) Console.WriteLine($"Using {parallelOptions.MaxDegreeOfParallelism} threads.");

        var stopwatch = Stopwatch.StartNew();

        if (settings.Verbose) Console.WriteLine($"Loading input file: {settings.InputFile}");
        var module = ModuleDefMD.Load(settings.InputFile);
        if (settings.Verbose) Console.WriteLine("Loaded input file.");

        CBuilder builder = minify
            ? new CMinifiedBuilder(true, toggleComments)
            : new CBeautifiedBuilder(true, toggleComments);

        var fields = new ConcurrentBag<FieldDef>();
        var methods = new ConcurrentBag<MethodDef>();
        var staticConstructors = new ConcurrentBag<MethodDef>();

        // First, emit the types (and load all fields and methods on the way)
        var cTypes = new ConcurrentDictionary<string, CType>();
        builder.AddComment("Types");

        Parallel.ForEach(module.Types, parallelOptions, type =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting type: {type.FullName}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(type.FullName);

            var cType = Emitter.EmitType(ref cBuilder, type, out var signature);
            cTypes.TryAdd(type.FullName, cType);

            lock (builder) builder.Append(cBuilder);

            foreach (var field in type.Fields)
            {
                if ((type.IsEnum && field.FieldType.FullName == signature.FullName) || !field.IsStatic) continue;
                fields.Add(field);
            }

            foreach (var method in type.Methods)
            {
                if (!method.IsStaticConstructor && method.DeclaringType.FullName != module.EntryPoint.DeclaringType.FullName) continue;
                if (method.IsStaticConstructor) staticConstructors.Add(method);
                methods.Add(method);
            }
        });

        if (settings.Verbose) Console.WriteLine($"Emitted {module.Types.Count} types, loaded {fields.Count} fields and {methods.Count} methods.");

        // Then, emit the method definitions
        builder.AddComment("Method definitions");

        Parallel.ForEach(methods, parallelOptions, method =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting method definition: {method.FullName}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(method.FullName);

            Emitter.EmitMethodDefinition(ref cBuilder, ref cTypes, method);

            lock (builder) builder.Append(cBuilder);
        });

        if (settings.Verbose) Console.WriteLine($"Emitted {methods.Count} methods.");

        // After that, emit the fields
        var cFields = new ConcurrentDictionary<string, CVariable>();
        builder.AddComment("Fields");

        Parallel.ForEach(fields, parallelOptions, field =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting field: {field.FullName}");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(field.FullName);

            var variable = Emitter.EmitField(ref cBuilder, ref cTypes, field);
            cFields.TryAdd(field.FullName, variable);

            lock (builder) builder.Append(cBuilder);
        });

        if (settings.Verbose) Console.WriteLine($"Emitted {fields.Count} fields.");

        // And finally, emit the actual method bodies
        builder.AddComment("Methods");

        Parallel.ForEach(methods, parallelOptions, method =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting method: {method.FullName}.");

            var cBuilder = builder.Clone();
            cBuilder.AddComment(method.FullName);

            Emitter.EmitMethod(ref cBuilder, ref cTypes, ref cFields, method);

            lock (builder) builder.Append(cBuilder);
        });

        if (settings.Verbose) Console.WriteLine("Emitting main function.");

        builder.AddComment("Entry point");
        Emitter.EmitMainFunction(ref builder, module.EntryPoint, ref staticConstructors);

        if (settings.Verbose) Console.WriteLine("Emitted main function.");

        stopwatch.Stop();
        if (settings.Verbose) Console.WriteLine($"Took {stopwatch.Elapsed.Milliseconds} ms ({stopwatch.Elapsed.Seconds} s).");

        if (settings.Verbose) Console.WriteLine("Saving file.");
        File.WriteAllText(settings.OutputFile, builder.ToString());
        if (settings.Verbose) Console.WriteLine("Saved file.");
    }
}