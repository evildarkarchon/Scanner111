namespace Scanner111.Common.Services.PathValidation;

/// <summary>
/// Utility class for checking if paths are in restricted system locations.
/// Restricted paths include Windows system directories that receive special
/// treatment by the OS (antivirus scrutiny, elevated permissions) which can
/// interfere with normal application operation.
/// </summary>
public static class RestrictedPathChecker
{
    private static readonly Lazy<IReadOnlyList<string>> RestrictedPaths = new(GetRestrictedPaths);

    /// <summary>
    /// Checks if the specified path is in a restricted system location.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is restricted, false otherwise.</returns>
    /// <remarks>
    /// Restricted locations include:
    /// <list type="bullet">
    /// <item><description>Windows directory (C:\Windows and subdirectories)</description></item>
    /// <item><description>Program Files directories</description></item>
    /// <item><description>ProgramData directory</description></item>
    /// <item><description>Recovery partition</description></item>
    /// <item><description>Recycle Bin</description></item>
    /// </list>
    /// </remarks>
    public static bool IsRestrictedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true; // Null/empty is considered restricted (fail-safe)
        }

        try
        {
            // Normalize the path to absolute form
            var normalizedPath = System.IO.Path.GetFullPath(path);

            // Case-insensitive comparison for Windows
            foreach (var restrictedPath in RestrictedPaths.Value)
            {
                if (IsPathOrSubdirectory(normalizedPath, restrictedPath))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // Invalid path format - consider it restricted
            return true;
        }
    }

    /// <summary>
    /// Checks if a path is equal to or a subdirectory of a base path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="basePath">The base path to compare against.</param>
    /// <returns>True if path equals basePath or is a subdirectory of it.</returns>
    private static bool IsPathOrSubdirectory(string path, string basePath)
    {
        // Ensure both paths end with directory separator for proper comparison
        var normalizedPath = NormalizePath(path);
        var normalizedBase = NormalizePath(basePath);

        return normalizedPath.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedBase + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a path by trimming trailing separators.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Gets the list of restricted paths for the current system.
    /// </summary>
    private static IReadOnlyList<string> GetRestrictedPaths()
    {
        var paths = new List<string>();

        // Windows directory (C:\Windows)
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windowsDir))
        {
            paths.Add(windowsDir);
        }

        // Program Files directories
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            paths.Add(programFiles);
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86) && !programFilesX86.Equals(programFiles, StringComparison.OrdinalIgnoreCase))
        {
            paths.Add(programFilesX86);
        }

        // ProgramData (C:\ProgramData)
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(programData))
        {
            paths.Add(programData);
        }

        // System drive root-level restricted directories
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

        // Recovery partition - typically hidden but worth checking
        paths.Add(System.IO.Path.Combine(systemDrive, "Recovery"));

        // Recycle Bin
        paths.Add(System.IO.Path.Combine(systemDrive, "$Recycle.Bin"));

        // System Volume Information
        paths.Add(System.IO.Path.Combine(systemDrive, "System Volume Information"));

        return paths.AsReadOnly();
    }

    /// <summary>
    /// Gets the list of restricted paths (for testing/debugging purposes).
    /// </summary>
    /// <returns>A read-only list of restricted path prefixes.</returns>
    public static IReadOnlyList<string> GetRestrictedPathsList()
    {
        return RestrictedPaths.Value;
    }
}
