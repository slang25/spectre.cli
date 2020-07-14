using System.Collections.Generic;

namespace Spectre.Cli.Internal
{
    /// <summary>
    /// Represents a command container.
    /// </summary>
    internal interface ICommandContainer
    {
        /// <summary>
        /// Gets all commands in the container.
        /// </summary>
        IList<CommandInfo> Commands { get; }
    }
}
