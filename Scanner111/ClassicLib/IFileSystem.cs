using System;
using System.IO;

namespace Scanner111.ClassicLib;

/// <summary>
/// Provides file system operations abstraction for better testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Gets the date and time the specified file was last written to.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>The date and time the specified file was last written to.</returns>
    DateTime GetLastWriteTime(string path);

    /// <summary>
    /// Reads all text from the specified file.
    /// </summary>
    /// <param name="path">The file path to read from.</param>
    /// <returns>The text content of the file.</returns>
    string ReadAllText(string path);

    /// <summary>
    /// Writes the specified text to a file.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="contents">The text to write.</param>
    void WriteAllText(string path, string contents);
}

/// <summary>
/// Default implementation of the IFileSystem interface using System.IO.
/// </summary>
public class FileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <inheritdoc />
    public DateTime GetLastWriteTime(string path)
    {
        return File.GetLastWriteTime(path);
    }

    /// <inheritdoc />
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents);
    }
}