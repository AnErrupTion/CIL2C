using System.Text;
using CCG;
using CCG.Expressions;
using CommandLine;
using dnlib.DotNet;

namespace CIL2C;

public static class Program
{
    public static void Main(string[] args)
    {
        var settings = Parser.Default.ParseArguments<Settings>(args).Value;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = (int)(Environment.ProcessorCount * 1.2) };
        if (settings.Verbose) Console.WriteLine($"Using {parallelOptions.MaxDegreeOfParallelism} threads.");

        if (settings.Verbose) Console.WriteLine($"Loading input file: {settings.InputFile}");

        var module = ModuleDefMD.Load(settings.InputFile);

        if (settings.Verbose) Console.WriteLine("Loaded input file.");

        var builder = new StringBuilder();
        builder.AppendLine("#include <stdint.h>");
        builder.AppendLine("#include <stdbool.h>");

        var fields = new List<FieldDef>();
        var methods = new List<MethodDef>();
        var staticConstructors = new List<MethodDef>();

        // First, emit the types (and load all fields and methods on the way)
        var cTypes = new Dictionary<string, CType>();
        
        Parallel.ForEach(module.Types, parallelOptions, type =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting type: {type.FullName}");

            var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
            var cType = emitter.EmitType(type, out var signature);

            lock (builder) builder.Append(emitter);

            cTypes.Add(type.FullName, cType);

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
        Parallel.ForEach(methods, parallelOptions, method =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting method definition: {method.FullName}");

            var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
            emitter.EmitMethodDefinition(ref cTypes, method);

            lock (builder) builder.Append(emitter);
        });

        if (settings.Verbose) Console.WriteLine($"Emitted {methods.Count} methods.");

        // After that, emit the fields
        var cFields = new Dictionary<string, CVariable>();

        Parallel.ForEach(fields, parallelOptions, field =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting field: {field.FullName}");

            var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
            var variable = emitter.EmitField(ref cTypes, field);

            lock (builder) builder.Append(emitter);

            cFields.Add(field.FullName, variable);
        });

        if (settings.Verbose) Console.WriteLine($"Emitted {fields.Count} fields.");

        // And finally, emit the actual method bodies
        Parallel.ForEach(methods, parallelOptions, method =>
        {
            if (settings.Verbose) Console.WriteLine($"Emitting method: {method.FullName}.");

            var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
            emitter.EmitMethod(ref cTypes, ref cFields, method);

            lock (builder) builder.Append(emitter);
        });

        if (settings.Verbose) Console.WriteLine("Emitting main function.");

        var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
        emitter.EmitMainFunction(module.EntryPoint, staticConstructors);
        builder.Append(emitter);

        if (settings.Verbose) Console.WriteLine("Emitted main function.");

        File.WriteAllText(settings.OutputFile, builder.ToString());
    }
}