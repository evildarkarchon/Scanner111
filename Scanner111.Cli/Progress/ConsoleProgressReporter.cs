using Scanner111.Common.Services.Orchestration;

namespace Scanner111.Cli.Progress;

/// <summary>
/// Reports scan progress to the console with a progress bar.
/// </summary>
public class ConsoleProgressReporter : IProgress<ScanProgress>
{
    private readonly object _lock = new();
    private int _lastLineLength;
    private DateTime _lastUpdate = DateTime.MinValue;
    private const int MinUpdateIntervalMs = 100;

    /// <summary>
    /// Reports progress to the console.
    /// </summary>
    public void Report(ScanProgress value)
    {
        // Throttle updates to avoid console flicker
        var now = DateTime.UtcNow;
        if ((now - _lastUpdate).TotalMilliseconds < MinUpdateIntervalMs
            && value.FilesProcessed < value.TotalFiles)
        {
            return;
        }
        _lastUpdate = now;

        lock (_lock)
        {
            // Build progress bar
            var percent = value.TotalFiles > 0
                ? (double)value.FilesProcessed / value.TotalFiles
                : 0;
            var barWidth = 30;
            var filled = (int)(percent * barWidth);
            var bar = new string('#', filled) + new string('-', barWidth - filled);

            // Format: Processing Crash Logs [########-----------------] 45/100
            var line = $"Processing Crash Logs [{bar}] {value.FilesProcessed}/{value.TotalFiles}";

            // Clear previous line and write new one
            Console.Write('\r');
            Console.Write(line);

            // Pad with spaces to clear any leftover characters
            if (line.Length < _lastLineLength)
            {
                Console.Write(new string(' ', _lastLineLength - line.Length));
            }
            _lastLineLength = line.Length;

            // Final newline when complete
            if (value.FilesProcessed >= value.TotalFiles)
            {
                Console.WriteLine();
            }
        }
    }
}
