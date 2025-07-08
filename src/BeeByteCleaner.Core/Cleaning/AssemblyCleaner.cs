using BeeByteCleaner.Core.Analysis;
using BeeByteCleaner.Core.Models;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;

namespace BeeByteCleaner.Core.Cleaning
{
    /// <summary>
    /// Main assembly cleaning service that orchestrates all cleaning operations.
    /// </summary>
    public class AssemblyCleaner : ICodeCleaner
    {
        private readonly ICodeAnalyzer _codeAnalyzer;
        private readonly StringDecryptor _stringDecryptor;
        private readonly MethodCleaner _methodCleaner;
        private readonly TypeCleaner _typeCleaner;

        /// <summary>
        /// Initializes a new instance of the AssemblyCleaner class.
        /// </summary>
        /// <param name="codeAnalyzer">The code analyzer to use for identifying live code.</param>
        public AssemblyCleaner(ICodeAnalyzer codeAnalyzer = null)
        {
            _codeAnalyzer = codeAnalyzer ?? new LiveCodeAnalyzer();
            _stringDecryptor = new StringDecryptor();
            _methodCleaner = new MethodCleaner();
            _typeCleaner = new TypeCleaner();
        }

        /// <summary>
        /// Cleans the specified assembly using the provided execution log.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to clean.</param>
        /// <param name="logFilePath">The path to the execution log file.</param>
        /// <returns>The result of the cleaning operation.</returns>
        public CleaningResult CleanAssembly(string assemblyPath, string logFilePath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                    return CleaningResult.Failure($"Assembly file not found: {assemblyPath}");

                if (!File.Exists(logFilePath))
                    return CleaningResult.Failure($"Log file not found: {logFilePath}");

                var executedMethods = new HashSet<string>(File.ReadAllLines(logFilePath));
                Console.WriteLine($"Read {executedMethods.Count} executed methods from log.");

                return CleanAssembly(assemblyPath, executedMethods);
            }
            catch (Exception ex)
            {
                return CleaningResult.Failure($"Error reading log file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cleans the specified assembly using the provided executed methods.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to clean.</param>
        /// <param name="executedMethods">The set of executed methods.</param>
        /// <returns>The result of the cleaning operation.</returns>
        public CleaningResult CleanAssembly(string assemblyPath, HashSet<string> executedMethods)
        {
            try
            {
                Console.WriteLine($"Mode: Cleaning '{Path.GetFileName(assemblyPath)}' for analysis (Decrypt, Invalidate, Reorder & Rename)...");
                
                var outputPath = Path.Combine(Path.GetDirectoryName(assemblyPath),
                    $"{Path.GetFileNameWithoutExtension(assemblyPath)}_cleaned.dll");

                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
                var readerParams = new ReaderParameters
                {
                    ReadWrite = true,
                    AssemblyResolver = resolver,
                    ReadingMode = ReadingMode.Immediate
                };

                using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParams))
                {
                    // Step 1: Identify live code
                    var (liveMethods, liveTypes) = _codeAnalyzer.IdentifyLiveCode(assembly, executedMethods);
                    Console.WriteLine($"Identified {liveMethods.Count} live methods and {liveTypes.Count} live types.");

                    // Step 2: Decrypt strings
                    Console.WriteLine("Decrypting strings...");
                    int decryptedCount = _stringDecryptor.DecryptStrings(assembly);
                    Console.WriteLine($"Decrypted and replaced {decryptedCount} string encryption calls.");

                    // Step 3: Invalidate unused method bodies
                    int invalidatedCount = _methodCleaner.InvalidateUnusedMethodBodies(assembly, liveMethods);
                    Console.WriteLine($"Invalidated (emptied) the bodies of {invalidatedCount} unused methods.");

                    // Step 4: Reorder methods to prioritize live code
                    Console.WriteLine("Reordering methods to prioritize live code...");
                    _methodCleaner.ReorderMethods(assembly, liveMethods);

                    // Step 5: Rename unused methods
                    Console.WriteLine("Renaming unused methods...");
                    int renamedMethodCount = _methodCleaner.RenameDeadMethods(assembly, liveMethods);
                    Console.WriteLine($"Renamed {renamedMethodCount} unused methods.");

                    // Step 6: Rename unused types
                    Console.WriteLine("Renaming unused types...");
                    int renamedTypeCount = _typeCleaner.RenameDeadTypes(assembly, liveTypes);
                    Console.WriteLine($"Renamed {renamedTypeCount} unused types.");

                    // Step 7: Save the cleaned assembly
                    Console.WriteLine($"\nSaving final analysis-purposed assembly to: {outputPath}");
                    var writerParams = new WriterParameters { WriteSymbols = false };
                    assembly.Write(outputPath, writerParams);
                    Console.WriteLine("Cleaning complete.");

                    return CleaningResult.Success(
                        outputPath,
                        liveMethods.Count,
                        liveTypes.Count,
                        decryptedCount,
                        invalidatedCount,
                        renamedMethodCount,
                        renamedTypeCount);
                }
            }
            catch (Exception ex)
            {
                return CleaningResult.Failure($"Error during assembly cleaning: {ex.Message}", ex);
            }
        }
    }
}
