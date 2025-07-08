using BeeByteCleaner.CLI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BeeByteCleaner.CLI.Services
{
    /// <summary>
    /// Processes command line arguments and executes the appropriate commands.
    /// </summary>
    public class CommandLineProcessor
    {
        private readonly Dictionary<string, ICommand> _commands;

        /// <summary>
        /// Initializes a new instance of the CommandLineProcessor class.
        /// </summary>
        public CommandLineProcessor()
        {
            _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase)
            {
                { "instrument", new InstrumentCommand() },
                { "clean", new CleanCommand() }
            };
        }

        /// <summary>
        /// Processes the command line arguments and executes the appropriate command.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The exit code (0 for success, non-zero for failure).</returns>
        public int ProcessArguments(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string commandName = args[0].ToLower();

            if (_commands.TryGetValue(commandName, out var command))
            {
                return command.Execute(args);
            }
            else
            {
                Console.WriteLine($"Error: Unknown command '{commandName}'.");
                PrintUsage();
                return 1;
            }
        }

        /// <summary>
        /// Prints the general usage information.
        /// </summary>
        private void PrintUsage()
        {
            Console.WriteLine("BeeByte Mono Deobfuscator");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  BeeByteCleaner.exe <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            
            foreach (var kvp in _commands.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  {kvp.Key,-12} {kvp.Value.Description}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  BeeByteCleaner.exe instrument Assembly-CSharp.dll");
            Console.WriteLine("  BeeByteCleaner.exe clean Assembly-CSharp.dll executed_methods.log");
            Console.WriteLine();
            Console.WriteLine("For more information about a specific command, run:");
            Console.WriteLine("  BeeByteCleaner.exe <command>");
        }

        /// <summary>
        /// Registers a new command with the processor.
        /// </summary>
        /// <param name="command">The command to register.</param>
        public void RegisterCommand(ICommand command)
        {
            _commands[command.CommandName] = command;
        }

        /// <summary>
        /// Gets all registered commands.
        /// </summary>
        /// <returns>A collection of all registered commands.</returns>
        public IEnumerable<ICommand> GetCommands()
        {
            return _commands.Values;
        }
    }
}
