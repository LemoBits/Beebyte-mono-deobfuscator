using BeeByteCleaner.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace BeeByteCleaner.Core.Cleaning
{
    /// <summary>
    /// Handles cleaning operations on methods, including invalidation, reordering, and renaming.
    /// </summary>
    public class MethodCleaner
    {
        /// <summary>
        /// Invalidates (empties) the bodies of unused methods.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <param name="liveMethods">The set of live methods that should not be invalidated.</param>
        /// <returns>The number of methods that were invalidated.</returns>
        public int InvalidateUnusedMethodBodies(AssemblyDefinition assembly, HashSet<string> liveMethods)
        {
            int count = 0;
            foreach (var method in assembly.MainModule.GetAllTypes().SelectMany(t => t.Methods))
            {
                if (!liveMethods.Contains(method.FullName) && InvalidateMethodBody(method))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Reorders methods within types to prioritize live code.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <param name="liveMethods">The set of live methods to prioritize.</param>
        public void ReorderMethods(AssemblyDefinition assembly, HashSet<string> liveMethods)
        {
            foreach (var type in assembly.MainModule.GetAllTypes())
            {
                if (type.Methods.Count <= 1) continue;

                var originalMethods = type.Methods.ToList();
                var liveMethodGroup = new List<MethodDefinition>();
                var deadMethodGroup = new List<MethodDefinition>();

                // Separate live and dead methods
                foreach (var method in originalMethods)
                {
                    if (liveMethods.Contains(method.FullName))
                        liveMethodGroup.Add(method);
                    else
                        deadMethodGroup.Add(method);
                }

                // Only reorder if we have both live and dead methods
                if (liveMethodGroup.Count == 0 || deadMethodGroup.Count == 0) continue;

                // Clear and re-add methods with live methods first
                type.Methods.Clear();
                foreach (var method in liveMethodGroup) 
                    type.Methods.Add(method);
                foreach (var method in deadMethodGroup) 
                    type.Methods.Add(method);
            }
        }

        /// <summary>
        /// Renames unused methods to generic names.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <param name="liveMethods">The set of live methods that should not be renamed.</param>
        /// <returns>The number of methods that were renamed.</returns>
        public int RenameDeadMethods(AssemblyDefinition assembly, HashSet<string> liveMethods)
        {
            int count = 0;
            foreach (var type in assembly.MainModule.GetAllTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (liveMethods.Contains(method.FullName)) continue;
                    if (method.IsConstructor || method.IsSpecialName) continue;
                    
                    method.Name = $"Method_{count++}";
                }
            }

            return count;
        }

        /// <summary>
        /// Invalidates a single method body by replacing it with a minimal implementation.
        /// </summary>
        /// <param name="method">The method to invalidate.</param>
        /// <returns>True if the method was successfully invalidated, false otherwise.</returns>
        private bool InvalidateMethodBody(MethodDefinition method)
        {
            if (!method.HasBody || method.IsAbstract) return false;

            var body = method.Body;
            var ilProcessor = body.GetILProcessor();

            // Clear existing method body
            body.Instructions.Clear();
            body.ExceptionHandlers.Clear();
            body.Variables.Clear();

            // Add appropriate return value for non-void methods
            if (method.ReturnType.MetadataType != MetadataType.Void)
            {
                var instructions = GetDefaultValueInstructions(method.ReturnType, ilProcessor);
                foreach (var instruction in instructions) 
                    ilProcessor.Append(instruction);
            }

            // Add return instruction
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
            return true;
        }

        /// <summary>
        /// Gets the IL instructions needed to load a default value for the specified type.
        /// </summary>
        /// <param name="typeRef">The type to get default value instructions for.</param>
        /// <param name="ilProcessor">The IL processor to create instructions with.</param>
        /// <returns>A list of instructions that load the default value for the type.</returns>
        private List<Instruction> GetDefaultValueInstructions(TypeReference typeRef, ILProcessor ilProcessor)
        {
            var instructions = new List<Instruction>();

            if (typeRef.IsValueType)
            {
                var resolvedType = typeRef.Resolve();
                if (resolvedType != null && resolvedType.IsEnum)
                {
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    return instructions;
                }

                switch (typeRef.MetadataType)
                {
                    case MetadataType.Boolean:
                    case MetadataType.Char:
                    case MetadataType.SByte:
                    case MetadataType.Byte:
                    case MetadataType.Int16:
                    case MetadataType.UInt16:
                    case MetadataType.Int32:
                    case MetadataType.UInt32:
                        instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        break;
                    case MetadataType.Int64:
                    case MetadataType.UInt64:
                        instructions.Add(Instruction.Create(OpCodes.Ldc_I8, 0L));
                        break;
                    case MetadataType.Single:
                        instructions.Add(Instruction.Create(OpCodes.Ldc_R4, 0.0f));
                        break;
                    case MetadataType.Double:
                        instructions.Add(Instruction.Create(OpCodes.Ldc_R8, 0.0d));
                        break;
                    default:
                        // For complex value types, use initobj
                        var tempLocal = new VariableDefinition(typeRef);
                        ilProcessor.Body.Variables.Add(tempLocal);
                        ilProcessor.Body.InitLocals = true;
                        instructions.Add(Instruction.Create(OpCodes.Ldloca_S, tempLocal));
                        instructions.Add(Instruction.Create(OpCodes.Initobj, typeRef));
                        instructions.Add(Instruction.Create(OpCodes.Ldloc, tempLocal));
                        break;
                }
            }
            else
            {
                // Reference types default to null
                instructions.Add(Instruction.Create(OpCodes.Ldnull));
            }

            return instructions;
        }
    }
}
