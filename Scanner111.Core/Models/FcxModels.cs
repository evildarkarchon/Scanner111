using System.Text;
using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Models;

/// <summary>
///     Result from FCX file integrity and game validation analysis
/// </summary>
public class FcxScanResult : AnalysisResult
{
    /// <summary>
    ///     File integrity check results
    /// </summary>
    public List<FileIntegrityCheck> FileChecks { get; set; } = new();

    /// <summary>
    ///     Hash validation results for game files
    /// </summary>
    public List<HashValidation> HashValidations { get; set; } = new();

    /// <summary>
    ///     Overall game integrity status
    /// </summary>
    public GameIntegrityStatus GameStatus { get; set; }

    /// <summary>
    ///     Game configuration used for the scan
    /// </summary>
    public GameConfiguration? GameConfig { get; set; }

    /// <summary>
    ///     Version compatibility warnings
    /// </summary>
    public List<string> VersionWarnings { get; set; } = new();

    /// <summary>
    ///     Recommended fixes for detected issues
    /// </summary>
    public List<string> RecommendedFixes { get; set; } = new();

    /// <summary>
    ///     Messages generated during FCX analysis
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    ///     Generate a formatted report of the FCX scan results
    /// </summary>
    public string GenerateReport()
    {
        var report = new StringBuilder();

        report.AppendLine("FCX Scan Results");
        report.AppendLine("================");
        report.AppendLine($"Game Status: {GameStatus}");
        report.AppendLine();

        if (FileChecks.Any())
        {
            report.AppendLine("File Integrity Checks:");
            foreach (var check in FileChecks)
            {
                report.AppendLine($"  - {check.FilePath}: {(check.IsValid ? "OK" : "FAILED")}");
                if (!check.IsValid && !string.IsNullOrEmpty(check.ErrorMessage))
                    report.AppendLine($"    Error: {check.ErrorMessage}");
            }

            report.AppendLine();
        }

        if (HashValidations.Any())
        {
            report.AppendLine("Hash Validations:");
            foreach (var validation in HashValidations)
                report.AppendLine($"  - {validation.FilePath}: {(validation.IsValid ? "OK" : "MISMATCH")}");
            report.AppendLine();
        }

        if (VersionWarnings.Any())
        {
            report.AppendLine("Version Warnings:");
            foreach (var warning in VersionWarnings) report.AppendLine($"  - {warning}");
            report.AppendLine();
        }

        if (RecommendedFixes.Any())
        {
            report.AppendLine("Recommended Fixes:");
            foreach (var fix in RecommendedFixes) report.AppendLine($"  - {fix}");
            report.AppendLine();
        }

        if (Messages.Any())
        {
            report.AppendLine("Analysis Messages:");
            foreach (var message in Messages) report.AppendLine($"  - {message}");
        }

        return report.ToString();
    }
}

/// <summary>
///     Status of file integrity check
/// </summary>
public enum FileStatus
{
    /// <summary>
    ///     File exists and is valid
    /// </summary>
    Valid,

    /// <summary>
    ///     File is missing
    /// </summary>
    Missing,

    /// <summary>
    ///     File exists but is invalid/corrupted
    /// </summary>
    Invalid,

    /// <summary>
    ///     File exists but with wrong version
    /// </summary>
    WrongVersion,

    /// <summary>
    ///     File has been modified
    /// </summary>
    Modified,

    /// <summary>
    ///     Unknown status
    /// </summary>
    Unknown
}

/// <summary>
///     Represents a file integrity check result
/// </summary>
public class FileIntegrityCheck
{
    /// <summary>
    ///     File path being checked
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    ///     Type of file (Executable, DLL, INI, etc.)
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the file exists
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    ///     Whether the file passed integrity checks
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     Error message if check failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    ///     File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     File last modified date
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    ///     Status of the file check
    /// </summary>
    public FileStatus Status { get; set; }

    /// <summary>
    ///     Detailed message about the check result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
///     Represents a hash validation result
/// </summary>
public class HashValidation
{
    /// <summary>
    ///     File path that was validated
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    ///     Expected hash value
    /// </summary>
    public string ExpectedHash { get; set; } = string.Empty;

    /// <summary>
    ///     Actual calculated hash value
    /// </summary>
    public string ActualHash { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the hash matches expected value
    /// </summary>
    public bool IsValid => string.Equals(ExpectedHash, ActualHash, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Type of hash algorithm used (SHA256, MD5, etc.)
    /// </summary>
    public string HashType { get; set; } = "SHA256";

    /// <summary>
    ///     Associated version information if applicable
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
///     Overall game integrity status
/// </summary>
public enum GameIntegrityStatus
{
    /// <summary>
    ///     Not yet checked
    /// </summary>
    Unknown,

    /// <summary>
    ///     All checks passed
    /// </summary>
    Good,

    /// <summary>
    ///     Minor issues detected but game should work
    /// </summary>
    Warning,

    /// <summary>
    ///     Critical issues detected that may cause crashes
    /// </summary>
    Critical,

    /// <summary>
    ///     Game installation not found or invalid
    /// </summary>
    Invalid
}

/// <summary>
///     Represents backup operation result
/// </summary>
public class BackupResult
{
    /// <summary>
    ///     Whether backup succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Path to backup location
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    ///     Files that were backed up
    /// </summary>
    public List<string> BackedUpFiles { get; set; } = new();

    /// <summary>
    ///     Total size of backup in bytes
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    ///     Error message if backup failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp of backup
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Progress tracking for backup operations
/// </summary>
public class BackupProgress
{
    /// <summary>
    ///     Current file being processed
    /// </summary>
    public string CurrentFile { get; set; } = string.Empty;

    /// <summary>
    ///     Total files to process
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    ///     Files processed so far
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    ///     Percentage complete
    /// </summary>
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;

    /// <summary>
    ///     Progress as a value between 0 and 1
    /// </summary>
    public double Progress => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles : 0;
}