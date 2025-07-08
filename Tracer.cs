using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
public static class BeeByteCleaner
{
    private static readonly ConcurrentDictionary<string, byte> _executedMethods = new ConcurrentDictionary<string, byte>();
    private static readonly string _logFilePath = Path.Combine(Environment.CurrentDirectory, "executed_methods.log");
    private static int _isInitialized = 0;
    private static int _isShutdown = 0;
    
    public static void LogExecution(string methodFullName)
    {
        if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 0)
        {
            Initialize();
        }
        
        _executedMethods.TryAdd(methodFullName, 0);
    }

    private static void Initialize()
    {
        Console.WriteLine("[RuntimeTracer] Initialized. Logging executed methods.");
        if (File.Exists(_logFilePath))
        {
            Console.WriteLine("[RuntimeTracer] Log file already exists, deleting...");
            File.Delete(_logFilePath);
        }
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object sender, EventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isShutdown, 1, 0) == 0)
        {
            Console.WriteLine($"[RuntimeTracer] Process exiting. Saving {_executedMethods.Count} executed method names to log file...");
            try
            {
                File.WriteAllLines(_logFilePath, _executedMethods.Keys);
                Console.WriteLine($"[RuntimeTracer] Log file saved successfully to: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeTracer] Error saving log file: {ex.Message}");
            }
        }
    }
}