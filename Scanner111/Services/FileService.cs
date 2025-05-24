using System.IO;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Implementation of file system operations.
/// </summary>
public class FileService : IFileService
{
    /// <summary>
    /// Copies a directory and all its contents to another location.
    /// </summary>
    /// <param name="sourceDir">The path of the source directory to be copied.</param>
    /// <param name="destDir">The path of the destination directory where the content should be copied.</param>
    public void CopyDirectory(string sourceDir, string destDir)
    {
        // Create the destination directory
        Directory.CreateDirectory(destDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, true);
        }

        // Get the subdirectories in the source directory and copy to the destination directory
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(directory, destSubDir);
        }
    }
}