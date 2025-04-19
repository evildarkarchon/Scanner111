// Scanner111.Core/Interfaces/Services/IFileSystemService.cs
namespace Scanner111.Core.Interfaces.Services;

/// <summary>
/// Provides an abstraction over file system operations to improve testability
/// and platform independence of the application.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    Task<bool> FileExistsAsync(string path);
    
    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    Task<bool> DirectoryExistsAsync(string path);
    
    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    Task<string> ReadAllTextAsync(string path);
    
    /// <summary>
    /// Writes all text to a file.
    /// </summary>
    Task WriteAllTextAsync(string path, string content);
    
    /// <summary>
    /// Reads all bytes from a file.
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string path);
    
    /// <summary>
    /// Writes all bytes to a file.
    /// </summary>
    Task WriteAllBytesAsync(string path, byte[]? bytes);
    
    /// <summary>
    /// Returns the names of files that match the specified search pattern.
    /// </summary>
    Task<string[]> GetFilesAsync(string path, string searchPattern, bool recursive = false);
    
    /// <summary>
    /// Returns the names of directories that match the specified search pattern.
    /// </summary>
    Task<string[]> GetDirectoriesAsync(string path, string searchPattern, bool recursive = false);
    
    /// <summary>
    /// Copies a file to a new location.
    /// </summary>
    Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false);
    
    /// <summary>
    /// Moves a file to a new location.
    /// </summary>
    Task MoveFileAsync(string sourcePath, string destinationPath);
    
    /// <summary>
    /// Creates a directory.
    /// </summary>
    Task CreateDirectoryAsync(string path);
    
    /// <summary>
    /// Deletes a file.
    /// </summary>
    Task DeleteFileAsync(string path);
    
    /// <summary>
    /// Deletes a directory.
    /// </summary>
    Task DeleteDirectoryAsync(string path, bool recursive = true);
}