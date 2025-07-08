namespace BeeByteCleaner.CLI.Commands
{
    /// <summary>
    /// Defines the contract for CLI commands.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command with the provided arguments.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The exit code (0 for success, non-zero for failure).</returns>
        int Execute(string[] args);

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Gets the description of the command.
        /// </summary>
        string Description { get; }
    }
}
