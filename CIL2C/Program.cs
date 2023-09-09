﻿using CommandLine;
using dnlib.DotNet;

namespace CIL2C;

public static class Program
{
    public static void Main(string[] args)
    {
        var settings = Parser.Default.ParseArguments<Settings>(args).Value;
        var module = ModuleDefMD.Load(settings.InputFile);
        var emitter = new Emitter(settings.Minify, settings.Minify ? settings.ToggleComments : !settings.ToggleComments);

        emitter.Emit(module
            .Types.First(x => x.Name == module.EntryPoint.DeclaringType.Name)
            .Methods.First(x => x.Name == "Add")
        );
        emitter.Emit(module.EntryPoint);
        emitter.EmitMainFunction(module.EntryPoint);

        File.WriteAllText(settings.OutputFile, emitter.ToString());
    }
}