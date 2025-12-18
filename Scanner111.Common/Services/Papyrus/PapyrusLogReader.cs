using System.Text;
using Scanner111.Common.Models.Papyrus;

namespace Scanner111.Common.Services.Papyrus;

/// <summary>
/// Reads and parses Papyrus log files incrementally to extract statistics.
/// </summary>
/// <remarks>
/// Uses streaming file I/O to handle large log files efficiently.
/// Only reads new content since the last read position.
/// Pattern detection matches the original Python implementation.
/// </remarks>
public sealed class PapyrusLogReader : IPapyrusLogReader
{
    private const string DumpsPattern = "Dumping Stacks";
    private const string StacksPattern = "Dumping Stack";
    private const string WarningPattern = " warning: ";
    private const string ErrorPattern = " error: ";

    /// <inheritdoc/>
    public long GetFileEndPosition(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        if (!File.Exists(logPath))
        {
            throw new FileNotFoundException("Papyrus log file not found.", logPath);
        }

        var fileInfo = new FileInfo(logPath);
        return fileInfo.Length;
    }

    /// <inheritdoc/>
    public async Task<PapyrusReadResult> ReadNewContentAsync(
        string logPath,
        long startPosition,
        PapyrusStats currentStats,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        ArgumentNullException.ThrowIfNull(currentStats);

        if (!File.Exists(logPath))
        {
            throw new FileNotFoundException("Papyrus log file not found.", logPath);
        }

        var dumps = currentStats.Dumps;
        var stacks = currentStats.Stacks;
        var warnings = currentStats.Warnings;
        var errors = currentStats.Errors;

        await using var stream = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite, // Allow reading while game is running
            bufferSize: 4096,
            useAsync: true);

        // Seek to the start position
        stream.Seek(startPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check for patterns - use ordinal comparison for performance
            // Note: "Dumping Stacks" (plural) comes before "Dumping Stack" (singular)
            // in this check since both contain "Dumping Stack"
            if (line.Contains(DumpsPattern, StringComparison.Ordinal))
            {
                dumps++;
            }
            else if (line.Contains(StacksPattern, StringComparison.Ordinal))
            {
                stacks++;
            }

            if (line.Contains(WarningPattern, StringComparison.OrdinalIgnoreCase))
            {
                warnings++;
            }

            if (line.Contains(ErrorPattern, StringComparison.OrdinalIgnoreCase))
            {
                errors++;
            }
        }

        var newStats = new PapyrusStats
        {
            Timestamp = DateTime.Now,
            Dumps = dumps,
            Stacks = stacks,
            Warnings = warnings,
            Errors = errors
        };

        return new PapyrusReadResult(newStats, stream.Position);
    }
}
