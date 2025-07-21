namespace Scanner111.CLI.Commands;

/// <summary>
///     Base interface for all CLI commands
/// </summary>
public interface ICommand<in T>
{
    Task<int> ExecuteAsync(T options);
}