using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class DynamicAnalysisTool
{
    public void Instrument(string dllPath)
    {
        Console.WriteLine($"Mode: Instrumenting '{Path.GetFileName(dllPath)}'...");
        var outputPath = Path.Combine(Path.GetDirectoryName(dllPath),
            $"{Path.GetFileNameWithoutExtension(dllPath)}_instrumented.dll");

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath));
        var readerParams = new ReaderParameters
            { ReadWrite = true, AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };

        using (var assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParams))
        {
            var logMethod = InjectTracer(assembly);

            int instrumentedMethodCount = 0;
            int failedMethodCount = 0;

            var allMethods = assembly.MainModule.GetAllTypes().SelectMany(t => t.Methods).ToList();

            foreach (var method in allMethods)
            {
                try
                {
                    if (!method.HasBody || method.DeclaringType.FullName == "DynamicAnalysis.RuntimeTracer") continue;

                    var ilProcessor = method.Body.GetILProcessor();
                    var firstInstruction = method.Body.Instructions.FirstOrDefault();
                    if (firstInstruction == null) continue;

                    ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Ldstr, method.FullName));
                    ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Call, logMethod));
                    instrumentedMethodCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to instrument method '{method.FullName}': {ex.Message}");
                    failedMethodCount++;
                }
            }

            Console.WriteLine($"\nSuccessfully instrumented {instrumentedMethodCount} methods.");
            if (failedMethodCount > 0) Console.WriteLine($"Skipped {failedMethodCount} methods due to errors.");

            Console.WriteLine($"Saving instrumented assembly to: {outputPath}");
            assembly.Write(outputPath);
            Console.WriteLine("Instrumentation complete.");
        }
    }

    public void CleanForAnalysis(string dllPath, string logFilePath)
    {
        Console.WriteLine(
            $"Mode: Cleaning '{Path.GetFileName(dllPath)}' for analysis (Decrypt, Invalidate, Reorder & Rename)...");
        var outputPath = Path.Combine(Path.GetDirectoryName(dllPath),
            $"{Path.GetFileNameWithoutExtension(dllPath)}_final_analysis.dll");

        var liveMethodsFromLog = new HashSet<string>(File.ReadAllLines(logFilePath));
        Console.WriteLine($"Read {liveMethodsFromLog.Count} executed methods from log.");

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath));
        var readerParams = new ReaderParameters
            { ReadWrite = true, AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };

        using (var assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParams))
        {
            var cleaner = new StaticCleanerLogic();

            var (liveMethods, liveTypes) = cleaner.IdentifyLiveCode(assembly, liveMethodsFromLog);
            Console.WriteLine($"Identified {liveMethods.Count} live methods and {liveTypes.Count} live types.");

            Console.WriteLine("Decrypting strings...");
            int decryptedCount = cleaner.DecryptStrings(assembly);
            Console.WriteLine($"Decrypted and replaced {decryptedCount} string encryption calls.");

            int invalidatedCount = cleaner.InvalidateUnusedMethodBodies(assembly, liveMethods);
            Console.WriteLine($"Invalidated (emptied) the bodies of {invalidatedCount} unused methods.");

            Console.WriteLine("Reordering methods to prioritize live code...");
            cleaner.ReorderMethods(assembly, liveMethods);

            Console.WriteLine("Renaming unused methods...");
            int renamedMethodCount = cleaner.RenameDeadMethods(assembly, liveMethods);
            Console.WriteLine($"Renamed {renamedMethodCount} unused methods.");

            Console.WriteLine("Renaming unused types...");
            int renamedTypeCount = cleaner.RenameDeadTypes(assembly, liveTypes);
            Console.WriteLine($"Renamed {renamedTypeCount} unused types.");

            Console.WriteLine($"\nSaving final analysis-purposed assembly to: {outputPath}");
            var writerParams = new WriterParameters { WriteSymbols = false };
            assembly.Write(outputPath, writerParams);
            Console.WriteLine("Cleaning complete.");
        }
    }

    private MethodReference InjectTracer(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;

        var existingTracer = module.GetType("DynamicAnalysis", "RuntimeTracer");
        if (existingTracer != null) return existingTracer.Methods.First(m => m.Name == "LogExecution");

        var tracerType = new TypeDefinition(
            "DynamicAnalysis", "RuntimeTracer",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            module.ImportReference(typeof(object)));
        module.Types.Add(tracerType);

        var stringType = module.ImportReference(typeof(string));
        var voidType = module.ImportReference(typeof(void));
        var concurrentDictType = module.ImportReference(typeof(ConcurrentDictionary<string, byte>));

        var executedMethodsField = new FieldDefinition("_executedMethods",
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, concurrentDictType);
        tracerType.Fields.Add(executedMethodsField);

        var logFilePathField = new FieldDefinition("_logFilePath",
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, stringType);
        tracerType.Fields.Add(logFilePathField);

        var logMethod =
            new MethodDefinition("LogExecution", MethodAttributes.Public | MethodAttributes.Static, voidType);
        logMethod.Parameters.Add(new ParameterDefinition("methodFullName", ParameterAttributes.None, stringType));
        tracerType.Methods.Add(logMethod);

        var il = logMethod.Body.GetILProcessor();
        var tryAddMethodRef = module.ImportReference(typeof(ConcurrentDictionary<string, byte>).GetMethod("TryAdd"));
        il.Emit(OpCodes.Ldsfld, executedMethodsField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Callvirt, tryAddMethodRef);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);

        var cctor = new MethodDefinition(".cctor",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.RTSpecialName |
            MethodAttributes.SpecialName, voidType);
        tracerType.Methods.Add(cctor);

        il = cctor.Body.GetILProcessor();
        var concurrentDictCtorRef =
            module.ImportReference(typeof(ConcurrentDictionary<string, byte>).GetConstructor(Type.EmptyTypes));
        var pathCombineRef =
            module.ImportReference(typeof(Path).GetMethod("Combine", new[] { typeof(string), typeof(string) }));
        var getCwdRef = module.ImportReference(typeof(Environment).GetProperty("CurrentDirectory").GetGetMethod());
        var getDomainRef = module.ImportReference(typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod());

        var processExitEventInfo = typeof(AppDomain).GetEvent("ProcessExit");
        var addProcessExitRef = module.ImportReference(processExitEventInfo.GetAddMethod());
        var onProcessExitMethod = InjectOnProcessExitMethod(module, tracerType, logFilePathField, executedMethodsField);
        var eventHandlerCtorRef =
            module.ImportReference(typeof(EventHandler).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));

        il.Emit(OpCodes.Newobj, concurrentDictCtorRef);
        il.Emit(OpCodes.Stsfld, executedMethodsField);
        il.Emit(OpCodes.Call, getCwdRef);
        il.Emit(OpCodes.Ldstr, "executed_methods.log");
        il.Emit(OpCodes.Call, pathCombineRef);
        il.Emit(OpCodes.Stsfld, logFilePathField);
        il.Emit(OpCodes.Call, getDomainRef);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldftn, onProcessExitMethod);
        il.Emit(OpCodes.Newobj, eventHandlerCtorRef);
        il.Emit(OpCodes.Callvirt, addProcessExitRef);
        il.Emit(OpCodes.Ret);

        return logMethod;
    }

    private MethodDefinition InjectOnProcessExitMethod(ModuleDefinition module, TypeDefinition tracerType,
        FieldDefinition logFilePathField, FieldDefinition executedMethodsField)
    {
        var onProcessExitMethod = new MethodDefinition("OnProcessExit",
            MethodAttributes.Private | MethodAttributes.Static, module.ImportReference(typeof(void)));
        onProcessExitMethod.Parameters.Add(new ParameterDefinition("sender", ParameterAttributes.None,
            module.ImportReference(typeof(object))));
        onProcessExitMethod.Parameters.Add(new ParameterDefinition("e", ParameterAttributes.None,
            module.ImportReference(typeof(EventArgs))));
        tracerType.Methods.Add(onProcessExitMethod);

        var il = onProcessExitMethod.Body.GetILProcessor();
        var keysPropertyRef =
            module.ImportReference(typeof(ConcurrentDictionary<string, byte>).GetProperty("Keys").GetGetMethod());
        var writeAllLinesRef = module.ImportReference(typeof(File).GetMethod("WriteAllLines",
            new[] { typeof(string), typeof(IEnumerable<string>) }));

        il.Emit(OpCodes.Ldsfld, logFilePathField);
        il.Emit(OpCodes.Ldsfld, executedMethodsField);
        il.Emit(OpCodes.Callvirt, keysPropertyRef);
        il.Emit(OpCodes.Call, writeAllLinesRef);
        il.Emit(OpCodes.Ret);

        return onProcessExitMethod;
    }
}