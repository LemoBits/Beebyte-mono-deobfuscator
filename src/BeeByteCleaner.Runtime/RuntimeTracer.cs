using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace BeeByteCleaner.Runtime
{
    /// <summary>
    /// Runtime tracer that logs method execution for dynamic analysis.
    /// This class is injected into instrumented assemblies to track method calls.
    /// </summary>
    public static class RuntimeTracer
    {
        private static readonly ConcurrentDictionary<string, byte> _executedMethods = new ConcurrentDictionary<string, byte>();
        private static readonly string _logFilePath = Path.Combine(Environment.CurrentDirectory, "executed_methods.log");
        private static int _isInitialized = 0;
        private static int _isShutdown = 0;
        
        /// <summary>
        /// Logs the execution of a method.
        /// </summary>
        /// <param name="methodFullName">The full name of the executed method.</param>
        public static void LogExecution(string methodFullName)
        {
            if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 0)
            {
                Initialize();
            }
            
            _executedMethods.TryAdd(methodFullName, 0);
        }

        /// <summary>
        /// Initializes the runtime tracer.
        /// </summary>
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

        /// <summary>
        /// Handles the process exit event to save the execution log.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
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
}
