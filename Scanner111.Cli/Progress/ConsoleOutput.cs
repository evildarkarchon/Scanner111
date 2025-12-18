using Scanner111.Common.Models.Configuration;

namespace Scanner111.Cli.Progress;

/// <summary>
/// Helper for console output with proper formatting.
/// </summary>
public static class ConsoleOutput
{
    private static readonly bool IsWindows = OperatingSystem.IsWindows();

    /// <summary>
    /// Writes the application header.
    /// </summary>
    public static void WriteHeader()
    {
        Console.WriteLine("Scanner111 - Crash Log Auto Scanner");
        Console.WriteLine(new string('-', 40));
    }

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    public static void WriteInfo(string message)
    {
        Console.WriteLine(StripEmoji(message));
    }

    /// <summary>
    /// Writes a warning message to stderr.
    /// </summary>
    public static void WriteWarning(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"Warning: {StripEmoji(message)}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Writes an error message to stderr.
    /// </summary>
    public static void WriteError(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {StripEmoji(message)}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Writes the scan summary.
    /// </summary>
    public static void WriteSummary(ScanResult result)
    {
        Console.WriteLine();
        Console.WriteLine("=== Scan Complete ===");
        Console.WriteLine($"  Total Files:  {result.Statistics.TotalFiles}");
        Console.WriteLine($"  Scanned:      {result.Statistics.Scanned}");
        Console.WriteLine($"  Incomplete:   {result.Statistics.Incomplete}");
        Console.WriteLine($"  Failed:       {result.Statistics.Failed}");
        Console.WriteLine($"  Duration:     {result.ScanDuration.TotalSeconds:F2}s");

        if (result.FailedLogs.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed logs:");
            foreach (var log in result.FailedLogs.Take(10))
            {
                Console.WriteLine($"  - {Path.GetFileName(log)}");
            }
            if (result.FailedLogs.Count > 10)
            {
                Console.WriteLine($"  ... and {result.FailedLogs.Count - 10} more");
            }
        }
    }

    /// <summary>
    /// Strips emoji characters on Windows (legacy console compatibility).
    /// </summary>
    private static string StripEmoji(string text)
    {
        if (!IsWindows || string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Remove emoji by filtering out surrogate pairs (astral plane characters)
        // This covers most emoji which are in the range U+10000 and above
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            // Skip high surrogate and its paired low surrogate (emoji are typically surrogate pairs)
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++; // Skip the low surrogate as well
                continue;
            }
            // Also skip misc symbols (U+2600-U+26FF) and dingbats (U+2700-U+27BF)
            if (c >= '\u2600' && c <= '\u27BF')
            {
                continue;
            }
            result.Append(c);
        }
        return result.ToString();
    }
}
