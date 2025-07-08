using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace BeeByteCleaner.Core.Extensions
{
    /// <summary>
    /// Extension methods for Mono.Cecil types to simplify common operations.
    /// </summary>
    public static class CecilExtensions
    {
        /// <summary>
        /// Gets all types in the module, including nested types.
        /// </summary>
        /// <param name="module">The module to get types from.</param>
        /// <returns>An enumerable of all type definitions in the module.</returns>
        public static IEnumerable<TypeDefinition> GetAllTypes(this ModuleDefinition module)
        {
            return module.Types.SelectMany(GetSelfAndNested);
        }

        /// <summary>
        /// Recursively gets a type and all its nested types.
        /// </summary>
        /// <param name="type">The type to get nested types from.</param>
        /// <returns>An enumerable containing the type and all its nested types.</returns>
        private static IEnumerable<TypeDefinition> GetSelfAndNested(TypeDefinition type)
        {
            var types = new List<TypeDefinition> { type };
            if (type.HasNestedTypes)
                types.AddRange(type.NestedTypes.SelectMany(GetSelfAndNested));
            return types;
        }
    }
}
