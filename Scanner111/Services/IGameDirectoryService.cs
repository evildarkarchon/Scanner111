using System.Threading.Tasks;

namespace Scanner111.Services;

/// <summary>
///     Interface for service that detects and manages game installation and documents directories
/// </summary>
public interface IGameDirectoryService
{
    /// <summary>
    ///     Gets the current game installation path
    /// </summary>
    string? GamePath { get; }

    /// <summary>
    ///     Gets the current game documents path
    /// </summary>
    string? DocsPath { get; }

    /// <summary>
    ///     Finds and configures the game installation directory
    /// </summary>
    /// <returns>A string with results or error messages</returns>
    Task<string> FindGamePathAsync();

    /// <summary>
    ///     Finds and configures the game documents directory (where config files and logs are stored)
    /// </summary>
    /// <returns>A string with results or error messages</returns>
    Task<string> FindDocsPathAsync();

    /// <summary>
    ///     Manually sets the game installation directory
    /// </summary>
    /// <param name="path">The path to set as the game directory</param>
    /// <returns>A result indicating success or failure</returns>
    Task<bool> SetGamePathManuallyAsync(string path);

    /// <summary>
    ///     Manually sets the game documents directory
    /// </summary>
    /// <param name="path">The path to set as the docs directory</param>
    /// <returns>A result indicating success or failure</returns>
    Task<bool> SetDocsPathManuallyAsync(string path);

    /// <summary>
    ///     Initializes all required paths for the application
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task InitializePathsAsync();
}