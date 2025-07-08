using BeeByteCleaner.Core.Extensions;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace BeeByteCleaner.Core.Cleaning
{
    /// <summary>
    /// Handles cleaning operations on types, including renaming unused types.
    /// </summary>
    public class TypeCleaner
    {
        /// <summary>
        /// Renames unused types to generic names.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <param name="liveTypes">The set of live types that should not be renamed.</param>
        /// <returns>The number of types that were renamed.</returns>
        public int RenameDeadTypes(AssemblyDefinition assembly, HashSet<string> liveTypes)
        {
            int count = 0;
            
            // Get types to rename, ordered by name length for consistent naming
            var typesToRename = assembly.MainModule.GetAllTypes()
                .Where(t => !liveTypes.Contains(t.FullName))
                .OrderBy(t => t.FullName.Length)
                .ToList();

            foreach (var type in typesToRename)
            {
                // Skip types that are already renamed
                if (type.Name.StartsWith("Type_")) continue;
                
                type.Name = $"Type_{count++}";
            }

            return count;
        }
    }
}
