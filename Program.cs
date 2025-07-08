using System;
using System.IO;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 2 || args[0].ToLower() != "clean")
        {
            PrintUsage();
            return;
        }

        string targetDllPath = args[1];
        if (!File.Exists(targetDllPath))
        {
            Console.WriteLine($"Error: File not found at '{targetDllPath}'");
            return;
        }

        if (args.Length < 3)
        {
            Console.WriteLine("Error: 'clean' mode requires a log file path.");
            PrintUsage();
            return;
        }

        string logFilePath = args[2];
        if (!File.Exists(logFilePath))
        {
            Console.WriteLine($"Error: Log file not found at '{logFilePath}'");
            return;
        }

        try
        {
            var tool = new DynamicAnalysisTool();
            tool.CleanForAnalysis(targetDllPath, logFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] An unexpected error occurred: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  BeeByteCleaner.exe clean <path_to_assembly.dll> <path_to_log_file.log>");
    }
}