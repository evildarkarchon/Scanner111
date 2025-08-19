using System.Diagnostics;

namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for retrieving file version information
/// </summary>
public interface IFileVersionInfoProvider
{
    /// <summary>
    /// Returns a FileVersionInfo representing the version information associated with the specified file
    /// </summary>
    /// <param name="fileName">The fully qualified path and name of the file to retrieve the version information for</param>
    /// <returns>A FileVersionInfo containing information about the file</returns>
    FileVersionInfo GetVersionInfo(string fileName);

    /// <summary>
    /// Attempts to get version information for the specified file
    /// </summary>
    /// <param name="fileName">The fully qualified path and name of the file to retrieve the version information for</param>
    /// <param name="versionInfo">When this method returns, contains the version info if successful; otherwise, null</param>
    /// <returns>true if the version information was successfully retrieved; otherwise, false</returns>
    bool TryGetVersionInfo(string fileName, out FileVersionInfo? versionInfo);
}