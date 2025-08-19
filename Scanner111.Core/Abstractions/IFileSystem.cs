using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for file system operations to enable testing and platform independence
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Determines whether the specified directory exists
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Returns the names of files in the specified directory that match the specified search pattern
    /// </summary>
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns the names of subdirectories in the specified directory
    /// </summary>
    string[] GetDirectories(string path);

    /// <summary>
    /// Opens a file for reading
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Opens or creates a file for writing
    /// </summary>
    Stream OpenWrite(string path);

    /// <summary>
    /// Creates all directories and subdirectories in the specified path
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Deletes the specified file
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes the specified directory
    /// </summary>
    void DeleteDirectory(string path, bool recursive = false);

    /// <summary>
    /// Copies an existing file to a new file
    /// </summary>
    void CopyFile(string source, string destination, bool overwrite = false);

    /// <summary>
    /// Moves a specified file to a new location
    /// </summary>
    void MoveFile(string source, string destination);

    /// <summary>
    /// Returns the date and time the specified file or directory was last written to
    /// </summary>
    DateTime GetLastWriteTime(string path);

    /// <summary>
    /// Gets the size of a file in bytes
    /// </summary>
    long GetFileSize(string path);

    /// <summary>
    /// Opens a text file, reads all text in the file, and then closes the file
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    /// Asynchronously opens a text file, reads all text in the file, and then closes the file
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new file, writes the specified string to the file, and then closes the file
    /// </summary>
    void WriteAllText(string path, string content);

    /// <summary>
    /// Asynchronously creates a new file, writes the specified string to the file, and then closes the file
    /// </summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
}