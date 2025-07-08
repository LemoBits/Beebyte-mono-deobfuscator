using System.Collections.Generic;
using Mono.Cecil;

namespace BeeByteCleaner.Core.Analysis
{
    /// <summary>
    /// Defines the contract for code analysis services.
    /// </summary>
    public interface ICodeAnalyzer
    {
        /// <summary>
        /// Identifies live code (methods and types) based on execution logs.
        /// </summary>
        /// <param name="assembly">The assembly to analyze.</param>
        /// <param name="executedMethods">The set of methods that were executed according to logs.</param>
        /// <returns>A tuple containing live methods and live types.</returns>
        (HashSet<string> liveMethods, HashSet<string> liveTypes) IdentifyLiveCode(
            AssemblyDefinition assembly, 
            HashSet<string> executedMethods);
    }
}
