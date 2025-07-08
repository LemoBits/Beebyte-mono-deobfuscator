using BeeByteCleaner.CLI.Services;
using System;

namespace BeeByteCleaner.CLI
{
    /// <summary>
    /// Main entry point for the BeeByteCleaner application.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point of the application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code (0 for success, non-zero for failure).</returns>
        public static int Main(string[] args)
        {
            try
            {
                var processor = new CommandLineProcessor();
                return processor.ProcessArguments(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] An unexpected error occurred: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }
    }
}
