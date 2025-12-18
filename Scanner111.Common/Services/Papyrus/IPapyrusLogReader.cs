using Scanner111.Common.Models.Papyrus;

namespace Scanner111.Common.Services.Papyrus;

/// <summary>
/// Result of reading new content from a Papyrus log file.
/// </summary>
/// <param name="Stats">The accumulated statistics from new content.</param>
/// <param name="NewPosition">The file position after reading.</param>
public readonly record struct PapyrusReadResult(PapyrusStats Stats, long NewPosition);

/// <summary>
/// Provides functionality for reading and parsing Papyrus log files incrementally.
/// </summary>
public interface IPapyrusLogReader
{
    /// <summary>
    /// Gets the current end-of-file position for the specified log file.
    /// </summary>
    /// <param name="logPath">The path to the Papyrus.0.log file.</param>
    /// <returns>The current file length (end position).</returns>
    /// <exception cref="FileNotFoundException">The log file does not exist.</exception>
    long GetFileEndPosition(string logPath);

    /// <summary>
    /// Reads new content from a Papyrus log file starting from a given position.
    /// </summary>
    /// <param name="logPath">The path to the Papyrus.0.log file.</param>
    /// <param name="startPosition">The position to start reading from.</param>
    /// <param name="currentStats">The current accumulated stats to add to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Updated statistics and new file position.</returns>
    /// <exception cref="FileNotFoundException">The log file does not exist.</exception>
    /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
    Task<PapyrusReadResult> ReadNewContentAsync(
        string logPath,
        long startPosition,
        PapyrusStats currentStats,
        CancellationToken cancellationToken = default);
}
