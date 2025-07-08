using BeeByteCleaner.Core.Extensions;
using BeeByteCleaner.Core.Models;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;

namespace BeeByteCleaner.Core.Instrumentation
{
    /// <summary>
    /// Main assembly instrumentation service that adds runtime tracing to methods.
    /// </summary>
    public class AssemblyInstrumentor : IInstrumentationService
    {
        private readonly RuntimeTracerInjector _tracerInjector;

        /// <summary>
        /// Initializes a new instance of the AssemblyInstrumentor class.
        /// </summary>
        public AssemblyInstrumentor()
        {
            _tracerInjector = new RuntimeTracerInjector();
        }

        /// <summary>
        /// Instruments the specified assembly to track method execution.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to instrument.</param>
        /// <returns>The result of the instrumentation operation.</returns>
        public InstrumentationResult InstrumentAssembly(string assemblyPath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                    return InstrumentationResult.Failure($"Assembly file not found: {assemblyPath}");

                Console.WriteLine($"Mode: Instrumenting '{Path.GetFileName(assemblyPath)}'...");
                
                var outputPath = Path.Combine(Path.GetDirectoryName(assemblyPath),
                    $"{Path.GetFileNameWithoutExtension(assemblyPath)}_instrumented.dll");

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
                    // Inject the runtime tracer
                    var logMethod = _tracerInjector.InjectTracer(assembly);

                    // Instrument all methods
                    var (instrumentedCount, failedCount) = InstrumentMethods(assembly, logMethod);

                    Console.WriteLine($"\nSuccessfully instrumented {instrumentedCount} methods.");
                    if (failedCount > 0) 
                        Console.WriteLine($"Skipped {failedCount} methods due to errors.");

                    // Save the instrumented assembly
                    Console.WriteLine($"Saving instrumented assembly to: {outputPath}");
                    assembly.Write(outputPath);
                    Console.WriteLine("Instrumentation complete.");

                    return InstrumentationResult.Success(outputPath, instrumentedCount, failedCount);
                }
            }
            catch (Exception ex)
            {
                return InstrumentationResult.Failure($"Error during assembly instrumentation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Instruments all methods in the assembly to call the logging method.
        /// </summary>
        /// <param name="assembly">The assembly to instrument.</param>
        /// <param name="logMethod">The logging method to call.</param>
        /// <returns>A tuple containing the number of successfully instrumented methods and failed methods.</returns>
        private (int instrumentedCount, int failedCount) InstrumentMethods(AssemblyDefinition assembly, MethodReference logMethod)
        {
            int instrumentedMethodCount = 0;
            int failedMethodCount = 0;

            var allMethods = assembly.MainModule.GetAllTypes().SelectMany(t => t.Methods).ToList();

            foreach (var method in allMethods)
            {
                try
                {
                    if (ShouldSkipMethod(method)) continue;

                    if (InstrumentMethod(method, logMethod))
                    {
                        instrumentedMethodCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to instrument method '{method.FullName}': {ex.Message}");
                    failedMethodCount++;
                }
            }

            return (instrumentedMethodCount, failedMethodCount);
        }

        /// <summary>
        /// Determines whether a method should be skipped during instrumentation.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method should be skipped, false otherwise.</returns>
        private bool ShouldSkipMethod(MethodDefinition method)
        {
            // Skip methods without bodies
            if (!method.HasBody) return true;

            // Skip the tracer itself to avoid infinite recursion
            if (method.DeclaringType.FullName == "DynamicAnalysis.RuntimeTracer") return true;

            return false;
        }

        /// <summary>
        /// Instruments a single method to call the logging method at the beginning.
        /// </summary>
        /// <param name="method">The method to instrument.</param>
        /// <param name="logMethod">The logging method to call.</param>
        /// <returns>True if the method was successfully instrumented, false otherwise.</returns>
        private bool InstrumentMethod(MethodDefinition method, MethodReference logMethod)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var firstInstruction = method.Body.Instructions.FirstOrDefault();
            if (firstInstruction == null) return false;

            // Insert logging call at the beginning of the method
            // Load the method's full name as a string
            ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Ldstr, method.FullName));
            // Call the logging method
            ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Call, logMethod));

            return true;
        }
    }
}
