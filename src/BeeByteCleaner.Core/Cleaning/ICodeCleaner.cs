using BeeByteCleaner.Core.Models;
using System.Collections.Generic;

namespace BeeByteCleaner.Core.Cleaning
{
    /// <summary>
    /// Defines the contract for assembly cleaning services.
    /// </summary>
    public interface ICodeCleaner
    {
        /// <summary>
        /// Cleans the specified assembly using the provided execution log.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to clean.</param>
        /// <param name="logFilePath">The path to the execution log file.</param>
        /// <returns>The result of the cleaning operation.</returns>
        CleaningResult CleanAssembly(string assemblyPath, string logFilePath);

        /// <summary>
        /// Cleans the specified assembly using the provided executed methods.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to clean.</param>
        /// <param name="executedMethods">The set of executed methods.</param>
        /// <returns>The result of the cleaning operation.</returns>
        CleaningResult CleanAssembly(string assemblyPath, HashSet<string> executedMethods);
    }
}
