using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for parsing Bethesda game crash logs.
/// </summary>
public interface ICrashLogParser
{
    /// <summary>
    /// Parses a crash log from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the crash log file to parse.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="CrashLog" /> instance containing the parsed crash log data, or null if the file is not a valid crash log.</returns>
    Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}