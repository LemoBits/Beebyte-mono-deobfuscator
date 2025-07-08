using BeeByteCleaner.Core.Extensions;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace BeeByteCleaner.Core.Analysis
{
    /// <summary>
    /// Analyzes assemblies to identify live code based on execution logs.
    /// </summary>
    public class LiveCodeAnalyzer : ICodeAnalyzer
    {
        /// <summary>
        /// Identifies live code (methods and types) based on execution logs.
        /// </summary>
        /// <param name="assembly">The assembly to analyze.</param>
        /// <param name="executedMethods">The set of methods that were executed according to logs.</param>
        /// <returns>A tuple containing live methods and live types.</returns>
        public (HashSet<string> liveMethods, HashSet<string> liveTypes) IdentifyLiveCode(
            AssemblyDefinition assembly, 
            HashSet<string> executedMethods)
        {
            var liveMethods = new HashSet<string>();
            var liveTypes = new HashSet<string>();
            
            // Create lookup dictionaries for efficient access
            var methodDefinitions = assembly.MainModule.GetAllTypes()
                .SelectMany(t => t.Methods)
                .Where(m => m != null)
                .GroupBy(m => m.FullName)
                .ToDictionary(g => g.Key, g => g.First());
                
            var typeDefinitions = assembly.MainModule.GetAllTypes()
                .Where(t => t != null)
                .GroupBy(t => t.FullName)
                .ToDictionary(g => g.Key, g => g.First());
            
            var methodQueue = new Queue<string>();
            var typeQueue = new Queue<string>();
            
            // Add root methods from execution log
            foreach (var rootMethod in executedMethods)
            {
                if (methodDefinitions.ContainsKey(rootMethod) && liveMethods.Add(rootMethod))
                {
                    methodQueue.Enqueue(rootMethod);
                }
            }

            // Add always-live types (public types, enums, Unity objects)
            foreach (var type in typeDefinitions.Values)
            {
                if ((type.IsPublic && !IsCompilerGenerated(type)) || 
                    type.IsEnum || 
                    InheritsFrom(type, "UnityEngine.Object"))
                {
                    if (liveTypes.Add(type.FullName)) 
                        typeQueue.Enqueue(type.FullName);
                }
            }

            // Process queues until no more dependencies are found
            while (methodQueue.Count > 0 || typeQueue.Count > 0)
            {
                ProcessMethodQueue(methodQueue, methodDefinitions, liveMethods, liveTypes, typeQueue);
                ProcessTypeQueue(typeQueue, typeDefinitions, liveTypes);
            }

            return (liveMethods, liveTypes);
        }

        /// <summary>
        /// Processes the method queue to find method dependencies.
        /// </summary>
        private void ProcessMethodQueue(Queue<string> methodQueue, 
            Dictionary<string, MethodDefinition> methodDefinitions,
            HashSet<string> liveMethods, HashSet<string> liveTypes, Queue<string> typeQueue)
        {
            while (methodQueue.Count > 0)
            {
                var methodFullName = methodQueue.Dequeue();
                if (!methodDefinitions.TryGetValue(methodFullName, out var methodDef)) 
                    continue;

                // Process method's declaring type, return type, and parameter types
                ProcessType(methodDef.DeclaringType, liveTypes, typeQueue);
                ProcessType(methodDef.ReturnType, liveTypes, typeQueue);
                
                foreach (var parameter in methodDef.Parameters)
                    ProcessType(parameter.ParameterType, liveTypes, typeQueue);

                // Process generic parameters and constraints
                if (methodDef.HasGenericParameters)
                {
                    foreach (var genericParam in methodDef.GenericParameters)
                    {
                        foreach (var constraint in genericParam.Constraints)
                            ProcessType(constraint.ConstraintType, liveTypes, typeQueue);
                    }
                }

                // Process method body instructions
                if (methodDef.HasBody)
                {
                    foreach (var instruction in methodDef.Body.Instructions)
                    {
                        // Add called methods to live methods
                        if (instruction.Operand is MethodReference calledMethodRef &&
                            liveMethods.Add(calledMethodRef.FullName))
                        {
                            methodQueue.Enqueue(calledMethodRef.FullName);
                        }

                        // Add referenced types to live types
                        if (instruction.Operand is TypeReference typeRef)
                            ProcessType(typeRef, liveTypes, typeQueue);

                        // Add field types to live types
                        if (instruction.Operand is FieldReference fieldRef)
                            ProcessType(fieldRef.FieldType, liveTypes, typeQueue);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the type queue to find type dependencies.
        /// </summary>
        private void ProcessTypeQueue(Queue<string> typeQueue, 
            Dictionary<string, TypeDefinition> typeDefinitions, 
            HashSet<string> liveTypes)
        {
            while (typeQueue.Count > 0)
            {
                var typeFullName = typeQueue.Dequeue();
                if (!typeDefinitions.TryGetValue(typeFullName, out var typeDef)) 
                    continue;

                // Process base type
                ProcessType(typeDef.BaseType, liveTypes, typeQueue);

                // Process interfaces
                if (typeDef.HasInterfaces)
                {
                    foreach (var interfaceImpl in typeDef.Interfaces)
                        ProcessType(interfaceImpl.InterfaceType, liveTypes, typeQueue);
                }

                // Process field types
                if (typeDef.HasFields)
                {
                    foreach (var field in typeDef.Fields)
                        ProcessType(field.FieldType, liveTypes, typeQueue);
                }

                // Process property types
                if (typeDef.HasProperties)
                {
                    foreach (var property in typeDef.Properties)
                        ProcessType(property.PropertyType, liveTypes, typeQueue);
                }

                // Process event types
                if (typeDef.HasEvents)
                {
                    foreach (var eventDef in typeDef.Events)
                        ProcessType(eventDef.EventType, liveTypes, typeQueue);
                }

                // Process custom attribute types
                if (typeDef.HasCustomAttributes)
                {
                    foreach (var attribute in typeDef.CustomAttributes)
                        ProcessType(attribute.AttributeType, liveTypes, typeQueue);
                }

                // Process generic parameters and constraints
                if (typeDef.HasGenericParameters)
                {
                    foreach (var genericParam in typeDef.GenericParameters)
                    {
                        foreach (var constraint in genericParam.Constraints)
                            ProcessType(constraint.ConstraintType, liveTypes, typeQueue);
                    }
                }
            }
        }

        /// <summary>
        /// Processes a type reference and adds it to live types if not already processed.
        /// </summary>
        private void ProcessType(TypeReference typeRef, HashSet<string> liveTypes, Queue<string> typeQueue)
        {
            if (typeRef == null || typeRef is GenericParameter) 
                return;

            if (typeRef is GenericInstanceType genericInstance)
            {
                ProcessType(genericInstance.ElementType, liveTypes, typeQueue);
                foreach (var arg in genericInstance.GenericArguments)
                    ProcessType(arg, liveTypes, typeQueue);
            }
            else if (liveTypes.Add(typeRef.FullName))
            {
                typeQueue.Enqueue(typeRef.FullName);
            }
        }

        /// <summary>
        /// Checks if a type inherits from a specific base type.
        /// </summary>
        private bool InheritsFrom(TypeDefinition type, string baseTypeName)
        {
            var current = type;
            while (current != null)
            {
                if (current.BaseType != null && current.BaseType.FullName == baseTypeName) 
                    return true;
                    
                try
                {
                    current = current.BaseType?.Resolve();
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is compiler-generated.
        /// </summary>
        private bool IsCompilerGenerated(TypeDefinition type)
        {
            if (type.Name.Contains("<") || type.Name.Contains(">")) 
                return true;
                
            return type.HasCustomAttributes && type.CustomAttributes.Any(a =>
                a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }
    }
}
