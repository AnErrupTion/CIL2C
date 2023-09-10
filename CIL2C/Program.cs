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

        var module = ModuleDefMD.Load(settings.InputFile);
        if (settings.Verbose) Console.WriteLine($"Loaded input file {settings.InputFile}.");

        var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);
        var staticConstructors = new List<MethodDef>();

        foreach (var type in module.Types)
        {
            var methods = new List<Tuple<MethodDef, CType, string, CVariable[]>>();
            foreach (var method in type.Methods)
            {
                if (!method.IsStaticConstructor &&
                    method.DeclaringType.FullName != module.EntryPoint.DeclaringType.FullName) continue;
                if (method.IsStaticConstructor) staticConstructors.Add(method);

                if (settings.Verbose) Console.WriteLine($"Emitting prototype: {method.FullName}");
                emitter.EmitPrototype(method, out var cType, out var safeName, out var arguments);

                methods.Add(Tuple.Create(method, cType, safeName, arguments));
            }

            foreach (var method in methods)
            {
                if (settings.Verbose) Console.WriteLine($"Emitting code: {method.Item1.FullName}");
                emitter.Emit(method.Item1, method.Item2, method.Item3, method.Item4);
            }
        }

        emitter.EmitMainFunction(module.EntryPoint, staticConstructors);

        File.WriteAllText(settings.OutputFile, emitter.ToString());
    }
}