using BeeByteCleaner.Core.Cleaning;
using System;
using System.IO;

namespace BeeByteCleaner.CLI.Commands
{
    /// <summary>
    /// Command for cleaning assemblies based on execution logs.
    /// </summary>
    public class CleanCommand : ICommand
    {
        private readonly ICodeCleaner _codeCleaner;

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName => "clean";

        /// <summary>
        /// Gets the description of the command.
        /// </summary>
        public string Description => "Cleans an assembly based on execution logs";

        /// <summary>
        /// Initializes a new instance of the CleanCommand class.
        /// </summary>
        /// <param name="codeCleaner">The code cleaner to use.</param>
        public CleanCommand(ICodeCleaner codeCleaner = null)
        {
            _codeCleaner = codeCleaner ?? new AssemblyCleaner();
        }

        /// <summary>
        /// Executes the clean command with the provided arguments.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The exit code (0 for success, non-zero for failure).</returns>
        public int Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Error: 'clean' command requires an assembly path and a log file path.");
                PrintUsage();
                return 1;
            }

            string assemblyPath = args[1];
            string logFilePath = args[2];

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"Error: Assembly file not found at '{assemblyPath}'");
                return 1;
            }

            if (!File.Exists(logFilePath))
            {
                Console.WriteLine($"Error: Log file not found at '{logFilePath}'");
                return 1;
            }

            try
            {
                var result = _codeCleaner.CleanAssembly(assemblyPath, logFilePath);

                if (result.IsSuccess)
                {
                    Console.WriteLine($"Cleaning completed successfully.");
                    Console.WriteLine($"Live methods: {result.LiveMethodCount}");
                    Console.WriteLine($"Live types: {result.LiveTypeCount}");
                    Console.WriteLine($"Decrypted strings: {result.DecryptedStringCount}");
                    Console.WriteLine($"Invalidated methods: {result.InvalidatedMethodCount}");
                    Console.WriteLine($"Renamed methods: {result.RenamedMethodCount}");
                    Console.WriteLine($"Renamed types: {result.RenamedTypeCount}");
                    Console.WriteLine($"Output saved to: {result.OutputPath}");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Cleaning failed: {result.ErrorMessage}");
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
        /// Prints the usage information for the clean command.
        /// </summary>
        private void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  BeeByteCleaner.exe clean <path_to_assembly.dll> <path_to_log_file.log>");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Cleans the specified assembly based on the execution log.");
            Console.WriteLine("  This will decrypt strings, remove unused code, and rename dead methods/types.");
        }
    }
}
