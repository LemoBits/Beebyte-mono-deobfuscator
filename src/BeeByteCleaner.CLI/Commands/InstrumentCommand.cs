using BeeByteCleaner.Core.Instrumentation;
using System;
using System.IO;

namespace BeeByteCleaner.CLI.Commands
{
    /// <summary>
    /// Command for instrumenting assemblies to track method execution.
    /// </summary>
    public class InstrumentCommand : ICommand
    {
        private readonly IInstrumentationService _instrumentationService;

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName => "instrument";

        /// <summary>
        /// Gets the description of the command.
        /// </summary>
        public string Description => "Instruments an assembly to track method execution";

        /// <summary>
        /// Initializes a new instance of the InstrumentCommand class.
        /// </summary>
        /// <param name="instrumentationService">The instrumentation service to use.</param>
        public InstrumentCommand(IInstrumentationService instrumentationService = null)
        {
            _instrumentationService = instrumentationService ?? new AssemblyInstrumentor();
        }

        /// <summary>
        /// Executes the instrument command with the provided arguments.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The exit code (0 for success, non-zero for failure).</returns>
        public int Execute(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: 'instrument' command requires an assembly path.");
                PrintUsage();
                return 1;
            }

            string assemblyPath = args[1];

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"Error: Assembly file not found at '{assemblyPath}'");
                return 1;
            }

            try
            {
                var result = _instrumentationService.InstrumentAssembly(assemblyPath);

                if (result.IsSuccess)
                {
                    Console.WriteLine($"Instrumentation completed successfully.");
                    Console.WriteLine($"Instrumented {result.InstrumentedMethodCount} methods.");
                    if (result.FailedMethodCount > 0)
                        Console.WriteLine($"Failed to instrument {result.FailedMethodCount} methods.");
                    Console.WriteLine($"Output saved to: {result.OutputPath}");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Instrumentation failed: {result.ErrorMessage}");
                    if (result.Exception != null)
                    {
                        Console.WriteLine($"Exception: {result.Exception.GetType().Name} - {result.Exception.Message}");
                    }
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] An unexpected error occurred: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// Prints the usage information for the instrument command.
        /// </summary>
        private void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  BeeByteCleaner.exe instrument <path_to_assembly.dll>");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Instruments the specified assembly to track method execution.");
            Console.WriteLine("  The instrumented assembly will log all executed methods to 'executed_methods.log'.");
        }
    }
}
