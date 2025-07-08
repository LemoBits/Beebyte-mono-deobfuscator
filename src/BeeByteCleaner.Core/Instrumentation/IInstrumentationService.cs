using BeeByteCleaner.Core.Models;

namespace BeeByteCleaner.Core.Instrumentation
{
    /// <summary>
    /// Defines the contract for assembly instrumentation services.
    /// </summary>
    public interface IInstrumentationService
    {
        /// <summary>
        /// Instruments the specified assembly to track method execution.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to instrument.</param>
        /// <returns>The result of the instrumentation operation.</returns>
        InstrumentationResult InstrumentAssembly(string assemblyPath);
    }
}
