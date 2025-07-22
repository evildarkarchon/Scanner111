namespace Scanner111.CLI.Commands;

/// <summary>
///     Base interface for all CLI commands
/// </summary>
public interface ICommand<in T>
{
    /// <summary>
    /// Executes the command logic asynchronously using the provided options.
    /// </summary>
    /// <param name="options">The options required to execute the command.</param>
    /// <returns>A task representing the asynchronous operation, returning an integer status code.</returns>
    Task<int> ExecuteAsync(T options);
}