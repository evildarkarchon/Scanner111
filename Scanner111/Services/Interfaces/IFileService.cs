namespace Scanner111.Services.Interfaces;

/// <summary>
///     Interface for file system operations.
/// </summary>
public interface IFileService
{
    /// <summary>
    ///     Copies a directory and all its contents to another location.
    /// </summary>
    /// <param name="sourceDir">The source directory path.</param>
    /// <param name="destDir">The destination directory path.</param>
    void CopyDirectory(string sourceDir, string destDir);
}