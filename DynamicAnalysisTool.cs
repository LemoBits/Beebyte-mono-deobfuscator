using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class DynamicAnalysisTool
{
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
}