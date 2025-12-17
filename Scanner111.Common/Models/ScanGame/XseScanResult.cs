namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of checking XSE (Script Extender) installation and integrity.
/// </summary>
public record XseScanResult
{
    /// <summary>
    /// Gets whether the Address Library is installed.
    /// </summary>
    public bool AddressLibraryInstalled { get; init; }

    /// <summary>
    /// Gets the Address Library check result details.
    /// </summary>
    public AddressLibraryStatus AddressLibraryStatus { get; init; } = AddressLibraryStatus.NotChecked;

    /// <summary>
    /// Gets whether XSE (Script Extender) is installed.
    /// </summary>
    public bool XseInstalled { get; init; }

    /// <summary>
    /// Gets the XSE installation status details.
    /// </summary>
    public XseInstallationStatus XseStatus { get; init; } = XseInstallationStatus.NotChecked;

    /// <summary>
    /// Gets the detected XSE version from the log file, if available.
    /// </summary>
    public string? DetectedVersion { get; init; }

    /// <summary>
    /// Gets whether the detected XSE version is the latest.
    /// </summary>
    public bool IsLatestVersion { get; init; }

    /// <summary>
    /// Gets errors found in the XSE log file.
    /// </summary>
    public IReadOnlyList<XseLogError> LogErrors { get; init; } = Array.Empty<XseLogError>();

    /// <summary>
    /// Gets script file integrity check results.
    /// </summary>
    public IReadOnlyList<ScriptHashResult> ScriptHashResults { get; init; } = Array.Empty<ScriptHashResult>();

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        !AddressLibraryInstalled ||
        !XseInstalled ||
        !IsLatestVersion ||
        LogErrors.Count > 0 ||
        ScriptHashResults.Any(s => s.Status != ScriptHashStatus.Valid);
}

/// <summary>
/// Represents the status of the Address Library installation.
/// </summary>
public enum AddressLibraryStatus
{
    /// <summary>
    /// Address Library check was not performed.
    /// </summary>
    NotChecked,

    /// <summary>
    /// Address Library is installed and valid.
    /// </summary>
    Installed,

    /// <summary>
    /// Address Library file is missing.
    /// </summary>
    Missing,

    /// <summary>
    /// Address Library path configuration is invalid.
    /// </summary>
    InvalidConfiguration
}

/// <summary>
/// Represents the status of XSE installation.
/// </summary>
public enum XseInstallationStatus
{
    /// <summary>
    /// XSE installation check was not performed.
    /// </summary>
    NotChecked,

    /// <summary>
    /// XSE is installed and the log file exists.
    /// </summary>
    Installed,

    /// <summary>
    /// XSE log file is missing (XSE may not be installed or never run).
    /// </summary>
    LogFileMissing,

    /// <summary>
    /// XSE log file path configuration is invalid.
    /// </summary>
    InvalidConfiguration
}

/// <summary>
/// Represents an error found in the XSE log file.
/// </summary>
/// <param name="LineNumber">The line number where the error was found.</param>
/// <param name="ErrorText">The error text from the log.</param>
/// <param name="MatchedPattern">The error pattern that matched.</param>
public record XseLogError(
    int LineNumber,
    string ErrorText,
    string MatchedPattern);

/// <summary>
/// Represents the result of checking a script file hash.
/// </summary>
/// <param name="FileName">The script file name (e.g., "Actor.pex").</param>
/// <param name="ExpectedHash">The expected SHA-256 hash.</param>
/// <param name="ActualHash">The actual SHA-256 hash, or null if file not found.</param>
/// <param name="Status">The hash verification status.</param>
public record ScriptHashResult(
    string FileName,
    string ExpectedHash,
    string? ActualHash,
    ScriptHashStatus Status);

/// <summary>
/// Represents the status of a script file hash verification.
/// </summary>
public enum ScriptHashStatus
{
    /// <summary>
    /// Script file exists and hash matches expected value.
    /// </summary>
    Valid,

    /// <summary>
    /// Script file is missing.
    /// </summary>
    Missing,

    /// <summary>
    /// Script file exists but hash does not match (outdated or modified).
    /// </summary>
    Mismatch,

    /// <summary>
    /// Could not read the script file (permission error, etc.).
    /// </summary>
    ReadError
}

/// <summary>
/// Configuration for XSE checking.
/// </summary>
public record XseConfiguration
{
    /// <summary>
    /// Gets the XSE acronym (e.g., "F4SE", "SKSE").
    /// </summary>
    public string Acronym { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full name of the script extender.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the latest version string.
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the path to the XSE log file (in Documents folder).
    /// </summary>
    public string? LogFilePath { get; init; }

    /// <summary>
    /// Gets the path to the Address Library file.
    /// </summary>
    public string? AddressLibraryPath { get; init; }

    /// <summary>
    /// Gets the path to the game's Scripts folder.
    /// </summary>
    public string? ScriptsFolderPath { get; init; }

    /// <summary>
    /// Gets the expected script file hashes (filename -> SHA-256 hash).
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpectedScriptHashes { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets error patterns to search for in the XSE log.
    /// </summary>
    public IReadOnlyList<string> LogErrorPatterns { get; init; } = Array.Empty<string>();
}
