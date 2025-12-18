namespace Scanner111.Cli;

/// <summary>
/// Standard exit codes for CLI operations.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Scan completed successfully (including scans with failed/incomplete logs - these are statistics, not errors).
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Scan path could not be found or auto-detected.
    /// </summary>
    public const int ScanPathNotFound = 2;

    /// <summary>
    /// Invalid CLI arguments provided.
    /// </summary>
    public const int InvalidArguments = 3;

    /// <summary>
    /// Operation was cancelled by user (Ctrl+C).
    /// </summary>
    public const int Cancelled = 4;

    /// <summary>
    /// An unexpected error occurred.
    /// </summary>
    public const int UnexpectedError = 99;
}
