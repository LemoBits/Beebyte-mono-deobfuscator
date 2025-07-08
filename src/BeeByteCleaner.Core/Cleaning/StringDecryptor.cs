using BeeByteCleaner.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeeByteCleaner.Core.Cleaning
{
    /// <summary>
    /// Handles decryption of obfuscated strings in assemblies.
    /// </summary>
    public class StringDecryptor
    {
        /// <summary>
        /// Decrypts strings in the specified assembly by finding and replacing decryption method calls.
        /// </summary>
        /// <param name="assembly">The assembly to decrypt strings in.</param>
        /// <returns>The number of strings that were successfully decrypted.</returns>
        public int DecryptStrings(AssemblyDefinition assembly)
        {
            var decryptionMethods = FindDecryptionMethods(assembly);
            if (!decryptionMethods.Any())
            {
                Console.WriteLine("[INFO] No string decryption methods found based on the defined logic.");
                return 0;
            }

            Console.WriteLine($"Found {decryptionMethods.Count} potential decryption method clones:");
            foreach (var method in decryptionMethods) 
                Console.WriteLine($"  - {method.FullName}");

            var decryptionMethodSet = new HashSet<MethodDefinition>(decryptionMethods);
            var callsToPatch = FindDecryptionCalls(assembly, decryptionMethodSet);

            int patchedCount = 0;
            if (callsToPatch.Any())
            {
                // Process in reverse order to avoid index issues when removing instructions
                foreach (var call in callsToPatch.AsEnumerable().Reverse())
                {
                    if (PatchDecryptionCall(call.methodBody, call.callInstruction))
                    {
                        patchedCount++;
                    }
                }
            }

            return patchedCount;
        }

        /// <summary>
        /// Finds all decryption methods in the assembly based on their signature and logic.
        /// </summary>
        private List<MethodDefinition> FindDecryptionMethods(AssemblyDefinition assembly)
        {
            var candidates = new List<MethodDefinition>();
            
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (IsDecryptionMethod(method))
                    {
                        candidates.Add(method);
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Checks if a method matches the signature and logic of a string decryption method.
        /// </summary>
        private bool IsDecryptionMethod(MethodDefinition method)
        {
            // Check method signature: static, public, returns string, takes two byte arrays
            if (!method.IsStatic || !method.IsPublic) return false;
            if (method.ReturnType.FullName != "System.String") return false;
            if (method.Parameters.Count != 2) return false;
            if (method.Parameters[0].ParameterType.FullName != "System.Byte[]") return false;
            if (method.Parameters[1].ParameterType.FullName != "System.Byte[]") return false;

            // Check if method body contains XOR operation (typical for decryption)
            if (!method.HasBody) return false;
            return method.Body.Instructions.Any(inst => inst.OpCode == OpCodes.Xor);
        }

        /// <summary>
        /// Finds all calls to decryption methods in the assembly.
        /// </summary>
        private List<(MethodBody methodBody, Instruction callInstruction)> FindDecryptionCalls(
            AssemblyDefinition assembly, HashSet<MethodDefinition> decryptionMethods)
        {
            var callsToPatch = new List<(MethodBody, Instruction)>();

            foreach (var type in assembly.MainModule.GetAllTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode == OpCodes.Call && 
                            instruction.Operand is MethodReference calledMethod)
                        {
                            var resolvedMethod = calledMethod.Resolve();
                            if (resolvedMethod != null && decryptionMethods.Contains(resolvedMethod))
                            {
                                callsToPatch.Add((method.Body, instruction));
                            }
                        }
                    }
                }
            }

            return callsToPatch;
        }

        /// <summary>
        /// Patches a single decryption call by extracting the encrypted data and replacing it with decrypted string.
        /// </summary>
        private bool PatchDecryptionCall(MethodBody methodBody, Instruction callInstruction)
        {
            try
            {
                var ilProcessor = methodBody.GetILProcessor();
                var instructions = methodBody.Instructions;

                int callIndex = instructions.IndexOf(callInstruction);
                if (callIndex < 2) return false;

                var extractionResult = ExtractByteArraysFromInstructions(instructions, callInstruction);
                if (extractionResult == null) return false;

                byte[] key = extractionResult.Item1;
                byte[] data = extractionResult.Item2;
                List<Instruction> instructionsToRemove = extractionResult.Item3;

                string decryptedString = DecryptData(key, data);

                // Replace the call instruction with a simple string load
                var newInstruction = ilProcessor.Create(OpCodes.Ldstr, decryptedString);
                ilProcessor.Replace(callInstruction, newInstruction);

                // Remove the instructions that were setting up the byte arrays
                foreach (var instruction in instructionsToRemove)
                {
                    if (methodBody.Instructions.Contains(instruction))
                        ilProcessor.Remove(instruction);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts byte arrays from the instruction sequence before a decryption call.
        /// </summary>
        private Tuple<byte[], byte[], List<Instruction>> ExtractByteArraysFromInstructions(
            Mono.Collections.Generic.Collection<Instruction> instructions, Instruction callInstruction)
        {
            var instructionsToRemove = new List<Instruction>();
            byte[] data = null;
            byte[] key = null;

            Instruction currentInstruction = callInstruction.Previous;

            // Extract the first byte array (data)
            var dataResult = FindAndExtractArrayPattern(currentInstruction, instructions);
            if (dataResult == null) return null;

            data = dataResult.Item1;
            instructionsToRemove.AddRange(dataResult.Item2);

            // Move to the instruction before the data array setup
            currentInstruction = dataResult.Item2.First().Previous;

            // Extract the second byte array (key)
            var keyResult = FindAndExtractArrayPattern(currentInstruction, instructions);
            if (keyResult == null) return null;

            key = keyResult.Item1;
            instructionsToRemove.AddRange(keyResult.Item2);

            if (key != null && data != null)
            {
                return new Tuple<byte[], byte[], List<Instruction>>(key, data, instructionsToRemove);
            }

            return null;
        }

        /// <summary>
        /// Finds and extracts a byte array initialization pattern from IL instructions.
        /// </summary>
        private Tuple<byte[], List<Instruction>> FindAndExtractArrayPattern(
            Instruction startInstruction, Mono.Collections.Generic.Collection<Instruction> allInstructions)
        {
            if (startInstruction == null || startInstruction.OpCode != OpCodes.Call ||
                !(startInstruction.Operand is MethodReference methodRef) ||
                methodRef.Name != "InitializeArray") 
                return null;

            var instructionsForThisArray = new List<Instruction> { startInstruction };

            // Find the ldtoken instruction (loads the field token)
            var ldtokenInstruction = startInstruction.Previous;
            if (ldtokenInstruction == null || ldtokenInstruction.OpCode != OpCodes.Ldtoken ||
                !(ldtokenInstruction.Operand is FieldReference fieldRef)) 
                return null;
            instructionsForThisArray.Add(ldtokenInstruction);

            // Get the field definition and its initial value (the byte array data)
            var fieldDef = fieldRef.Resolve();
            if (fieldDef == null || fieldDef.InitialValue == null) 
                return null;

            var byteArray = fieldDef.InitialValue;

            // Find the dup instruction
            var dupInstruction = ldtokenInstruction.Previous;
            if (dupInstruction == null || dupInstruction.OpCode != OpCodes.Dup) 
                return null;
            instructionsForThisArray.Add(dupInstruction);

            // Find the newarr instruction (creates new array)
            var newarrInstruction = dupInstruction.Previous;
            if (newarrInstruction == null || newarrInstruction.OpCode != OpCodes.Newarr) 
                return null;
            instructionsForThisArray.Add(newarrInstruction);

            // Find the ldc instruction (loads array size)
            var ldcInstruction = newarrInstruction.Previous;
            if (ldcInstruction == null || !ldcInstruction.OpCode.Name.StartsWith("ldc.i4")) 
                return null;
            instructionsForThisArray.Add(ldcInstruction);

            // Reverse the list to get the correct order
            instructionsForThisArray.Reverse();
            return new Tuple<byte[], List<Instruction>>(byteArray, instructionsForThisArray);
        }

        /// <summary>
        /// Decrypts data using XOR with the provided key.
        /// </summary>
        private string DecryptData(byte[] key, byte[] data)
        {
            byte[] decryptedData = new byte[data.Length];
            Array.Copy(data, decryptedData, data.Length);

            // XOR decrypt
            for (int i = 0; i < decryptedData.Length; i++)
            {
                decryptedData[i] ^= key[i % key.Length];
            }

            string resultString = Encoding.UTF8.GetString(decryptedData);

            // Handle special terminator character
            int terminatorIndex = resultString.IndexOf('\ue44f');
            if (terminatorIndex >= 0)
            {
                return resultString.Substring(0, terminatorIndex);
            }

            return resultString;
        }
    }
}
