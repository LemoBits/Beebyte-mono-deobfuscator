using System;
using System.Collections.Generic;

namespace BeeByteCleaner.Core.Models
{
    /// <summary>
    /// Represents the result of an assembly cleaning operation.
    /// </summary>
    public class CleaningResult
    {
        /// <summary>
        /// Gets or sets whether the cleaning was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the path to the cleaned assembly.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Gets or sets the number of live methods identified.
        /// </summary>
        public int LiveMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the number of live types identified.
        /// </summary>
        public int LiveTypeCount { get; set; }

        /// <summary>
        /// Gets or sets the number of strings that were decrypted.
        /// </summary>
        public int DecryptedStringCount { get; set; }

        /// <summary>
        /// Gets or sets the number of unused method bodies that were invalidated.
        /// </summary>
        public int InvalidatedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the number of unused methods that were renamed.
        /// </summary>
        public int RenamedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the number of unused types that were renamed.
        /// </summary>
        public int RenamedTypeCount { get; set; }

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred during cleaning, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Creates a successful cleaning result.
        /// </summary>
        public static CleaningResult Success(string outputPath, int liveMethodCount, int liveTypeCount, 
            int decryptedStringCount, int invalidatedMethodCount, int renamedMethodCount, int renamedTypeCount)
        {
            return new CleaningResult
            {
                IsSuccess = true,
                OutputPath = outputPath,
                LiveMethodCount = liveMethodCount,
                LiveTypeCount = liveTypeCount,
                DecryptedStringCount = decryptedStringCount,
                InvalidatedMethodCount = invalidatedMethodCount,
                RenamedMethodCount = renamedMethodCount,
                RenamedTypeCount = renamedTypeCount
            };
        }

        /// <summary>
        /// Creates a failed cleaning result.
        /// </summary>
        public static CleaningResult Failure(string errorMessage, Exception exception = null)
        {
            return new CleaningResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}
