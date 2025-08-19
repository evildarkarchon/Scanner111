namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for path manipulation operations
/// </summary>
public interface IPathService
{
    /// <summary>
    /// Combines strings into a path
    /// </summary>
    string Combine(params string[] paths);

    /// <summary>
    /// Returns the directory information for the specified path string
    /// </summary>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Returns the file name and extension of the specified path string
    /// </summary>
    string GetFileName(string path);

    /// <summary>
    /// Returns the file name of the specified path string without the extension
    /// </summary>
    string GetFileNameWithoutExtension(string path);

    /// <summary>
    /// Returns the extension of the specified path string
    /// </summary>
    string GetExtension(string path);

    /// <summary>
    /// Returns the absolute path for the specified path string
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// Normalizes a path for the current platform (handles separators, environment variables, etc.)
    /// </summary>
    string NormalizePath(string path);

    /// <summary>
    /// Gets a value indicating whether the specified path string contains a root
    /// </summary>
    bool IsPathRooted(string path);
    
    /// <summary>
    /// Gets the platform-specific directory separator character
    /// </summary>
    char DirectorySeparatorChar { get; }
}