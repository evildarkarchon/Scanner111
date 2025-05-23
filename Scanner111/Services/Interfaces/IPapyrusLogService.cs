namespace Scanner111.Services.Interfaces;

/// <summary>
///     Interface for analyzing Papyrus log files and extracting statistics.
/// </summary>
public interface IPapyrusLogService
{
    /// <summary>
    ///     Analyzes Papyrus log files, extracting various statistics and compiling a summary.
    ///     This method reads a Papyrus log file, if available, and computes key data such
    ///     as the total number of dumps, stacks, warnings, and errors present in the log.
    ///     It also calculates the ratio of dumps to stacks. If the log file is not found,
    ///     the method provides user guidance on enabling and locating Papyrus logging.
    /// </summary>
    /// <returns>
    ///     A tuple containing a formatted string with log analysis details and the total count of dumps extracted from
    ///     the log.
    /// </returns>
    (string Message, int DumpCount) AnalyzePapyrusLog();
}