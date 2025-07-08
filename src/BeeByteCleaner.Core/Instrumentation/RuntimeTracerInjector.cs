using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BeeByteCleaner.Core.Instrumentation
{
    /// <summary>
    /// Handles injection of runtime tracer code into assemblies.
    /// </summary>
    public class RuntimeTracerInjector
    {
        private const string TracerNamespace = "DynamicAnalysis";
        private const string TracerTypeName = "RuntimeTracer";

        /// <summary>
        /// Injects the runtime tracer into the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to inject the tracer into.</param>
        /// <returns>A reference to the LogExecution method that can be called to log method execution.</returns>
        public MethodReference InjectTracer(AssemblyDefinition assembly)
        {
            var module = assembly.MainModule;

            // Check if tracer already exists
            var existingTracer = module.GetType(TracerNamespace, TracerTypeName);
            if (existingTracer != null) 
                return existingTracer.Methods.First(m => m.Name == "LogExecution");

            // Create the tracer type
            var tracerType = CreateTracerType(module);
            module.Types.Add(tracerType);

            // Create fields
            var executedMethodsField = CreateExecutedMethodsField(module, tracerType);
            var logFilePathField = CreateLogFilePathField(module, tracerType);

            // Create the LogExecution method
            var logMethod = CreateLogExecutionMethod(module, tracerType, executedMethodsField);

            // Create the static constructor
            CreateStaticConstructor(module, tracerType, executedMethodsField, logFilePathField);

            return logMethod;
        }

        /// <summary>
        /// Creates the main tracer type definition.
        /// </summary>
        private TypeDefinition CreateTracerType(ModuleDefinition module)
        {
            return new TypeDefinition(
                TracerNamespace, 
                TracerTypeName,
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
                module.ImportReference(typeof(object)));
        }

        /// <summary>
        /// Creates the field that stores executed methods.
        /// </summary>
        private FieldDefinition CreateExecutedMethodsField(ModuleDefinition module, TypeDefinition tracerType)
        {
            var concurrentDictType = module.ImportReference(typeof(ConcurrentDictionary<string, byte>));
            var executedMethodsField = new FieldDefinition("_executedMethods",
                FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, 
                concurrentDictType);
            tracerType.Fields.Add(executedMethodsField);
            return executedMethodsField;
        }

        /// <summary>
        /// Creates the field that stores the log file path.
        /// </summary>
        private FieldDefinition CreateLogFilePathField(ModuleDefinition module, TypeDefinition tracerType)
        {
            var stringType = module.ImportReference(typeof(string));
            var logFilePathField = new FieldDefinition("_logFilePath",
                FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, 
                stringType);
            tracerType.Fields.Add(logFilePathField);
            return logFilePathField;
        }

        /// <summary>
        /// Creates the LogExecution method that will be called to log method execution.
        /// </summary>
        private MethodDefinition CreateLogExecutionMethod(ModuleDefinition module, TypeDefinition tracerType, 
            FieldDefinition executedMethodsField)
        {
            var stringType = module.ImportReference(typeof(string));
            var voidType = module.ImportReference(typeof(void));

            var logMethod = new MethodDefinition("LogExecution", 
                MethodAttributes.Public | MethodAttributes.Static, voidType);
            logMethod.Parameters.Add(new ParameterDefinition("methodFullName", ParameterAttributes.None, stringType));
            tracerType.Methods.Add(logMethod);

            // Generate method body
            var il = logMethod.Body.GetILProcessor();
            var tryAddMethodRef = module.ImportReference(typeof(ConcurrentDictionary<string, byte>).GetMethod("TryAdd"));
            
            il.Emit(OpCodes.Ldsfld, executedMethodsField);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, tryAddMethodRef);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);

            return logMethod;
        }

        /// <summary>
        /// Creates the static constructor that initializes the tracer.
        /// </summary>
        private void CreateStaticConstructor(ModuleDefinition module, TypeDefinition tracerType,
            FieldDefinition executedMethodsField, FieldDefinition logFilePathField)
        {
            var voidType = module.ImportReference(typeof(void));
            var cctor = new MethodDefinition(".cctor",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.RTSpecialName |
                MethodAttributes.SpecialName, voidType);
            tracerType.Methods.Add(cctor);

            var il = cctor.Body.GetILProcessor();

            // Initialize the concurrent dictionary
            var concurrentDictCtorRef = module.ImportReference(
                typeof(ConcurrentDictionary<string, byte>).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Newobj, concurrentDictCtorRef);
            il.Emit(OpCodes.Stsfld, executedMethodsField);

            // Set up the log file path
            var pathCombineRef = module.ImportReference(
                typeof(Path).GetMethod("Combine", new[] { typeof(string), typeof(string) }));
            var getCwdRef = module.ImportReference(
                typeof(Environment).GetProperty("CurrentDirectory").GetGetMethod());
            
            il.Emit(OpCodes.Call, getCwdRef);
            il.Emit(OpCodes.Ldstr, "executed_methods.log");
            il.Emit(OpCodes.Call, pathCombineRef);
            il.Emit(OpCodes.Stsfld, logFilePathField);

            // Register the process exit handler
            var getDomainRef = module.ImportReference(
                typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod());
            var processExitEventInfo = typeof(AppDomain).GetEvent("ProcessExit");
            var addProcessExitRef = module.ImportReference(processExitEventInfo.GetAddMethod());
            var onProcessExitMethod = CreateOnProcessExitMethod(module, tracerType, logFilePathField, executedMethodsField);
            var eventHandlerCtorRef = module.ImportReference(
                typeof(EventHandler).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));

            il.Emit(OpCodes.Call, getDomainRef);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldftn, onProcessExitMethod);
            il.Emit(OpCodes.Newobj, eventHandlerCtorRef);
            il.Emit(OpCodes.Callvirt, addProcessExitRef);
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates the OnProcessExit method that saves the execution log when the process exits.
        /// </summary>
        private MethodDefinition CreateOnProcessExitMethod(ModuleDefinition module, TypeDefinition tracerType,
            FieldDefinition logFilePathField, FieldDefinition executedMethodsField)
        {
            var onProcessExitMethod = new MethodDefinition("OnProcessExit",
                MethodAttributes.Private | MethodAttributes.Static, module.ImportReference(typeof(void)));
            onProcessExitMethod.Parameters.Add(new ParameterDefinition("sender", ParameterAttributes.None,
                module.ImportReference(typeof(object))));
            onProcessExitMethod.Parameters.Add(new ParameterDefinition("e", ParameterAttributes.None,
                module.ImportReference(typeof(EventArgs))));
            tracerType.Methods.Add(onProcessExitMethod);

            var il = onProcessExitMethod.Body.GetILProcessor();
            var keysPropertyRef = module.ImportReference(
                typeof(ConcurrentDictionary<string, byte>).GetProperty("Keys").GetGetMethod());
            var writeAllLinesRef = module.ImportReference(typeof(File).GetMethod("WriteAllLines",
                new[] { typeof(string), typeof(IEnumerable<string>) }));

            il.Emit(OpCodes.Ldsfld, logFilePathField);
            il.Emit(OpCodes.Ldsfld, executedMethodsField);
            il.Emit(OpCodes.Callvirt, keysPropertyRef);
            il.Emit(OpCodes.Call, writeAllLinesRef);
            il.Emit(OpCodes.Ret);

            return onProcessExitMethod;
        }
    }
}
