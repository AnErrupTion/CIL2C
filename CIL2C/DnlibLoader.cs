using CCG;
using CCG.Expressions;
using CIL2C.TypeSystem;
using dnlib.DotNet;

namespace CIL2C;

public static class DnlibLoader
{
    public static CilModule LoadModuleDef(ModuleDefMD moduleDef)
    {
        var moduleDefTypes = moduleDef.GetTypes().ToArray();

        var externalIncludes = new List<string>();
        var types = new Dictionary<string, CilType>();
        var allStaticFields = new Dictionary<string, CilField>();
        var allStaticNonEnumFields = new Dictionary<string, CilField>();
        var allNonStaticFields = new Dictionary<string, CilField>();
        var allMethods = new Dictionary<string, CilMethod>();
        var allBodiedMethods = new Dictionary<string, CilMethod>();
        var allStaticConstructors = new Dictionary<string, CilMethod>();
 
        CilMethod? entryPoint = null;

        foreach (var type in moduleDefTypes)
        {
            var fields = new Dictionary<string, CilField>();

            var typeSafeName = Utils.GetSafeName(type.Name);
            var typeSafeFullName = Utils.GetSafeName(type.FullName);
            var cilType = new CilType(
                typeSafeFullName,
                typeSafeName,
                GetCType(type.FullName, typeSafeFullName),
                type.IsEnum,
                type is { IsClass: true, IsEnum: false },
                type is { IsValueType: true, IsEnum: false },
                fields,
                type.CustomAttributes
            );

            types.Add(type.FullName, cilType);
        }

        foreach (var type in moduleDefTypes)
        {
            var cilType = types[type.FullName];

            foreach (var field in type.Fields)
            {
                var fieldSafeName = Utils.GetSafeName(field.Name);
                var fieldSafeFullName = Utils.GetSafeName(field.FullName);
                var fieldType = GetCilType(ref types, field.FieldType.FullName);
                var cilField = new CilField(
                    cilType,
                    fieldType,
                    fieldSafeFullName,
                    fieldSafeName,
                    field.IsStatic,
                    new CVariable(field.HasConstant, false, fieldType.CType, fieldSafeFullName),
                    field.HasConstant ? field.Constant.Value : null
                );

                if (field.IsStatic)
                {
                    if (!type.IsEnum || field.FieldType.FullName != type.FullName) allStaticNonEnumFields.Add(field.FullName, cilField);
                    allStaticFields.Add(field.FullName, cilField);
                }
                else allNonStaticFields.Add(field.FullName, cilField);

                cilType.Fields.Add(field.FullName, cilField);
            }

            types[type.FullName] = cilType;

            foreach (var method in type.Methods)
            {
                // TODO: Remove this line once we can emit newarr
                if (!method.IsStaticConstructor
                    && method.DeclaringType.FullName != moduleDef.EntryPoint.DeclaringType.FullName) continue;

                CilMethodBody? cilMethodBody = null;
                var needsExternalCFunction = false;
                string? externalCFunctionName = null;

                if (method.HasBody)
                {
                    var cilLocals = new List<CilLocal>();
                    foreach (var variable in method.Body.Variables)
                    {
                        var localType = GetCilType(ref types, variable.Type.FullName);
                        var localName = string.IsNullOrEmpty(variable.Name) ? $"local{variable.Index}" : variable.Name;
                        var local = new CilLocal(
                            localType,
                            new CVariable(false, false, localType.CType, localName)
                        );

                        cilLocals.Add(local);
                    }

                    cilMethodBody = new CilMethodBody(
                        method.Body.MaxStack,
                        method.Body.InitLocals,
                        cilLocals,
                        method.Body.Instructions
                    );
                }
                else
                {
                    foreach (var attribute in method.CustomAttributes)
                    {
                        if (!attribute.TypeFullName.EndsWith("CFunctionImportAttribute")) continue;

                        var includeFile = Convert.ToString(attribute.GetField("IncludeFile").Value);
                        var functionName = Convert.ToString(attribute.GetField("FunctionName").Value);

                        if (includeFile is null || functionName is null) continue;

                        needsExternalCFunction = true;
                        externalCFunctionName = functionName;

                        if (!externalIncludes.Contains(includeFile)) externalIncludes.Add(includeFile);
                        break;
                    }
                }

                var cilMethodArguments = new List<CilMethodArgument>();
                foreach (var parameter in method.Parameters)
                {
                    var argumentType = GetCilType(ref types, parameter.Type.FullName);
                    var argument = new CilMethodArgument(
                        argumentType,
                        parameter.Name
                    );

                    cilMethodArguments.Add(argument);
                }

                var cilMethod = new CilMethod(
                    cilType,
                    GetCilType(ref types, method.ReturnType.FullName),
                    Utils.GetSafeName(method.FullName),
                    Utils.GetSafeName(method.Name),
                    needsExternalCFunction,
                    externalCFunctionName,
                    cilMethodArguments,
                    cilMethodBody
                );

                if (method.FullName == moduleDef.EntryPoint.FullName) entryPoint = cilMethod;
                if (method.IsStaticConstructor) allStaticConstructors.Add(method.FullName, cilMethod);
                if (method.HasBody || needsExternalCFunction) allBodiedMethods.Add(method.FullName, cilMethod);

                allMethods.Add(method.FullName, cilMethod);
            }
        }

        if (entryPoint is null) throw new EntryPointNotFoundException();

        return new CilModule(
            Utils.GetSafeName(moduleDef.FullName),
            moduleDef.Name,
            entryPoint,
            externalIncludes,
            types,
            allStaticFields,
            allStaticNonEnumFields,
            allNonStaticFields,
            allMethods,
            allBodiedMethods,
            allStaticConstructors
        );
    }

    // The documentation says pointers are native int, but that doesn't make sense since addresses can't be negative
    private static CType GetCType(
        string fullName,
        string safeFullName
    ) => fullName.EndsWith('*') || fullName.EndsWith("[]") ? Utils.UIntPtr : new CType(safeFullName);

    private static CilType GetCilType(
        ref Dictionary<string, CilType> types,
        string fullName
    ) => fullName.EndsWith('*') || fullName.EndsWith("[]") ? types["System.UIntPtr"] : types[fullName];
}