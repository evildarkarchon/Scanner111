using Scanner111.Core.Models;

/// <summary>
///     Interface for moving unsolved logs
/// </summary>
public interface IUnsolvedLogsMover
{
    /// <summary>
    ///     Move an unsolved/incomplete crash log and its autoscan report to backup directory
    /// </summary>
    /// <param name="crashLogPath">Path to the crash log file</param>
    /// <param name="settings">Application settings to use for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully moved, false otherwise</returns>
    Task<bool> MoveUnsolvedLogAsync(string crashLogPath, ApplicationSettings? settings = null,
        CancellationToken cancellationToken = default);
}