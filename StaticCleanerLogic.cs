using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

public class StaticCleanerLogic
{
    public (HashSet<string> liveMethods, HashSet<string> liveTypes) IdentifyLiveCode(AssemblyDefinition assembly, HashSet<string> rootMethodsFromLog)
    {
        var liveMethods = new HashSet<string>();
        var liveTypes = new HashSet<string>();
        
        var methodDefinitions = assembly.MainModule.GetAllTypes().SelectMany(t => t.Methods).Where(m => m != null).GroupBy(m => m.FullName).ToDictionary(g => g.Key, g => g.First());
        var typeDefinitions = assembly.MainModule.GetAllTypes().Where(t => t != null).GroupBy(t => t.FullName).ToDictionary(g => g.Key, g => g.First());
        
        var methodQueue = new Queue<string>();
        var typeQueue = new Queue<string>();

        foreach (var rootMethod in rootMethodsFromLog)
        {
            if (methodDefinitions.ContainsKey(rootMethod) && liveMethods.Add(rootMethod))
            {
                methodQueue.Enqueue(rootMethod);
            }
        }
        
        foreach(var type in typeDefinitions.Values)
        {
            if (type.IsPublic || type.IsEnum || InheritsFrom(type, "UnityEngine.Object"))
            {
                if(liveTypes.Add(type.FullName))
                    typeQueue.Enqueue(type.FullName);
            }
        }

        while (methodQueue.Count > 0 || typeQueue.Count > 0)
        {
            while (methodQueue.Count > 0)
            {
                var methodFullName = methodQueue.Dequeue();
                if (!methodDefinitions.TryGetValue(methodFullName, out var methodDef)) continue;
                ProcessType(methodDef.DeclaringType, liveTypes, typeQueue);
                ProcessType(methodDef.ReturnType, liveTypes, typeQueue);
                foreach (var p in methodDef.Parameters) ProcessType(p.ParameterType, liveTypes, typeQueue);
                if (methodDef.HasGenericParameters) foreach (var gp in methodDef.GenericParameters) foreach(var c in gp.Constraints) ProcessType(c.ConstraintType, liveTypes, typeQueue);
                
                if (methodDef.HasBody)
                {
                    foreach (var instruction in methodDef.Body.Instructions)
                    {
                        if (instruction.Operand is MethodReference calledMethodRef && liveMethods.Add(calledMethodRef.FullName)) methodQueue.Enqueue(calledMethodRef.FullName);
                        if (instruction.Operand is TypeReference typeRef) ProcessType(typeRef, liveTypes, typeQueue);
                        if (instruction.Operand is FieldReference fieldRef) ProcessType(fieldRef.FieldType, liveTypes, typeQueue);
                    }
                }
            }

            while (typeQueue.Count > 0)
            {
                var typeFullName = typeQueue.Dequeue();
                if (!typeDefinitions.TryGetValue(typeFullName, out var typeDef)) continue;
                
                ProcessType(typeDef.BaseType, liveTypes, typeQueue);
                if (typeDef.HasInterfaces) foreach (var i in typeDef.Interfaces) ProcessType(i.InterfaceType, liveTypes, typeQueue);
                if (typeDef.HasFields) foreach (var f in typeDef.Fields) ProcessType(f.FieldType, liveTypes, typeQueue);
                if (typeDef.HasProperties) foreach (var p in typeDef.Properties) ProcessType(p.PropertyType, liveTypes, typeQueue);
                if (typeDef.HasEvents) foreach (var e in typeDef.Events) ProcessType(e.EventType, liveTypes, typeQueue);
                if (typeDef.HasCustomAttributes) foreach (var a in typeDef.CustomAttributes) ProcessType(a.AttributeType, liveTypes, typeQueue);
                if (typeDef.HasGenericParameters) foreach (var gp in typeDef.GenericParameters) foreach (var c in gp.Constraints) ProcessType(c.ConstraintType, liveTypes, typeQueue);
            }
        }
        return (liveMethods, liveTypes);
    }
    
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

    public void ReorderMethods(AssemblyDefinition assembly, HashSet<string> liveMethods)
    {
        foreach (var type in assembly.MainModule.GetAllTypes())
        {
            if (type.Methods.Count <= 1) continue;
            var originalMethods = type.Methods.ToList();
            var liveMethodGroup = new List<MethodDefinition>();
            var deadMethodGroup = new List<MethodDefinition>();

            foreach (var method in originalMethods)
            {
                if (liveMethods.Contains(method.FullName)) liveMethodGroup.Add(method);
                else deadMethodGroup.Add(method);
            }

            if (liveMethodGroup.Count == 0 || deadMethodGroup.Count == 0) continue;
            
            type.Methods.Clear();
            foreach(var method in liveMethodGroup) type.Methods.Add(method);
            foreach(var method in deadMethodGroup) type.Methods.Add(method);
        }
    }

    public int RenameDeadMethods(AssemblyDefinition assembly, HashSet<string> liveMethods)
    {
        int count = 0;
        foreach (var type in assembly.MainModule.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                if (liveMethods.Contains(method.FullName)) continue;
                if (method.IsConstructor || method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn) continue;
                
                method.Name = $"Method_{count++}";
            }
        }
        return count;
    }

    public int RenameDeadTypes(AssemblyDefinition assembly, HashSet<string> liveTypes)
    {
        int count = 0;
        var typesToRename = assembly.MainModule.GetAllTypes()
            .Where(t => !liveTypes.Contains(t.FullName))
            .OrderBy(t => t.FullName.Length)
            .ToList();

        foreach (var type in typesToRename)
        {
            if (type.Name.StartsWith("Type_")) continue;
            
            type.Name = $"Type_{count++}";
        }
        return count;
    }

    private bool InvalidateMethodBody(MethodDefinition method)
    {
        if (!method.HasBody || method.IsAbstract) return false;
        var body = method.Body;
        var ilProcessor = body.GetILProcessor();
        body.Instructions.Clear();
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        if (method.ReturnType.MetadataType != MetadataType.Void)
        {
            var instructions = GetDefaultValueInstructions(method.ReturnType, ilProcessor);
            foreach (var instr in instructions) ilProcessor.Append(instr);
        }
        ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        return true;
    }
    
    private List<Instruction> GetDefaultValueInstructions(TypeReference typeRef, ILProcessor ilProcessor)
    {
        var instructions = new List<Instruction>();
        if (typeRef.IsValueType)
        {
            var resolvedType = typeRef.Resolve();
            if (resolvedType != null && resolvedType.IsEnum) { instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0)); return instructions; }
            switch (typeRef.MetadataType)
            {
                case MetadataType.Boolean: case MetadataType.Char: case MetadataType.SByte: case MetadataType.Byte:
                case MetadataType.Int16: case MetadataType.UInt16: case MetadataType.Int32: case MetadataType.UInt32:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0)); break;
                case MetadataType.Int64: case MetadataType.UInt64:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I8, 0L)); break;
                case MetadataType.Single:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_R4, 0.0f)); break;
                case MetadataType.Double:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_R8, 0.0d)); break;
                default:
                    var tempLocal = new VariableDefinition(typeRef);
                    ilProcessor.Body.Variables.Add(tempLocal);
                    ilProcessor.Body.InitLocals = true;
                    instructions.Add(Instruction.Create(OpCodes.Ldloca_S, tempLocal));
                    instructions.Add(Instruction.Create(OpCodes.Initobj, typeRef));
                    instructions.Add(Instruction.Create(OpCodes.Ldloc, tempLocal));
                    break;
            }
        }
        else instructions.Add(Instruction.Create(OpCodes.Ldnull));
        return instructions;
    }

    private void ProcessType(TypeReference typeRef, HashSet<string> liveTypes, Queue<string> typeQueue)
    {
        if (typeRef == null || typeRef is GenericParameter) return;
        if (typeRef is GenericInstanceType genericInstance)
        {
            ProcessType(genericInstance.ElementType, liveTypes, typeQueue);
            foreach (var arg in genericInstance.GenericArguments) ProcessType(arg, liveTypes, typeQueue);
        }
        else if (liveTypes.Add(typeRef.FullName))
        {
            typeQueue.Enqueue(typeRef.FullName);
        }
    }

    private bool InheritsFrom(TypeDefinition type, string baseTypeName)
    {
        var current = type;
        while (current != null)
        {
            if (current.BaseType != null && current.BaseType.FullName == baseTypeName) return true;
            try { current = current.BaseType?.Resolve(); }
            catch { return false; }
        }
        return false;
    }
}