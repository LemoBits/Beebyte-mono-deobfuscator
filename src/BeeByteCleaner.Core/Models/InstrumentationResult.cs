using System;

namespace BeeByteCleaner.Core.Models
{
    /// <summary>
    /// Represents the result of an assembly instrumentation operation.
    /// </summary>
    public class InstrumentationResult
    {
        /// <summary>
        /// Gets or sets whether the instrumentation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the path to the instrumented assembly.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Gets or sets the number of methods that were successfully instrumented.
        /// </summary>
        public int InstrumentedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the number of methods that failed to be instrumented.
        /// </summary>
        public int FailedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred during instrumentation, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Creates a successful instrumentation result.
        /// </summary>
        public static InstrumentationResult Success(string outputPath, int instrumentedCount, int failedCount)
        {
            return new InstrumentationResult
            {
                IsSuccess = true,
                OutputPath = outputPath,
                InstrumentedMethodCount = instrumentedCount,
                FailedMethodCount = failedCount
            };
        }

        /// <summary>
        /// Creates a failed instrumentation result.
        /// </summary>
        public static InstrumentationResult Failure(string errorMessage, Exception exception = null)
        {
            return new InstrumentationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}
