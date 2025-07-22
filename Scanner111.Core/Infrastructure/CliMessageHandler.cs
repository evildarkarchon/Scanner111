using System.Runtime.InteropServices;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Handles console-based messages with optional colored output,
/// supporting info, warning, error, debug, success, and critical notifications.
/// </summary>
public class CliMessageHandler : IMessageHandler
{
    internal static CliProgress? ActiveProgress;
    private readonly string _currentLogFile;
    private readonly string _logDirectory;
    private readonly bool _useColors;

    public CliMessageHandler(bool useColors = true)
    {
        _useColors = useColors && SupportsColors();

        // Set up debug log directory
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111", "DebugLogs");

        Directory.CreateDirectory(_logDirectory);

        // Create current log file with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        _currentLogFile = Path.Combine(_logDirectory, $"scanner111-debug-{timestamp}.log");

        // Clean up old log files (keep only 10)
        CleanupOldLogFiles();
    }

    /// <summary>
    /// Displays an informational message to the console with a cyan-colored prefix ("ℹ️ INFO")
    /// unless the target is set to GUI only.
    /// </summary>
    /// <param name="message">The informational message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be displayed.
    /// Defaults to <see cref="MessageTarget.All"/>, which displays the message on both console and GUI.
    /// </param>
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("ℹ️ INFO", message, ConsoleColor.Cyan);
    }

    /// <summary>
    /// Displays a warning message to the console with a yellow-colored prefix ("⚠️ WARNING")
    /// unless the target is set to GUI only.
    /// </summary>
    /// <param name="message">The warning message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be displayed.
    /// Defaults to <see cref="MessageTarget.All"/>, which displays the message on both console and GUI.
    /// </param>
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("⚠️ WARNING", message, ConsoleColor.Yellow);
    }

    /// <summary>
    /// Displays an error message to the console with a red-colored prefix ("❌ ERROR")
    /// unless the target is set to GUI only.
    /// </summary>
    /// <param name="message">The error message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be displayed.
    /// Defaults to <see cref="MessageTarget.All"/>, which displays the message on both console and GUI.
    /// </param>
    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("❌ ERROR", message, ConsoleColor.Red);
    }

    /// <summary>
    /// Displays a success message to the console with a green-colored prefix ("✅ SUCCESS")
    /// unless the target is set to GUI only.
    /// </summary>
    /// <param name="message">The success message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be displayed.
    /// Defaults to <see cref="MessageTarget.All"/>, which displays the message on both console and GUI.
    /// </param>
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("✅ SUCCESS", message, ConsoleColor.Green);
    }

    /// <summary>
    /// Displays a debug message to the console or writes it to a file, depending on the specified target.
    /// If the target is set to GUI only, the message will not be displayed or logged.
    /// </summary>
    /// <param name="message">The debug message to be displayed or logged.</param>
    /// <param name="target">
    /// Specifies the destination for the debug message. Defaults to <see cref="MessageTarget.All"/>,
    /// which logs the message and displays it on the console.
    /// </param>
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteDebugToFile(message);
    }

    /// <summary>
    /// Displays a critical message to the console with a magenta-colored prefix ("🚨 CRITICAL")
    /// unless the target is set to GUI only.
    /// </summary>
    /// <param name="message">The critical message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be displayed. Defaults to <see cref="MessageTarget.All"/>,
    /// which displays the message on both console and GUI.
    /// </param>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("🚨 CRITICAL", message, ConsoleColor.Magenta);
    }

    /// <summary>
    /// Displays a message to the console or other targets, with optional details, based on the specified message type and target.
    /// </summary>
    /// <param name="message">The main message to be displayed.</param>
    /// <param name="details">Optional additional details related to the message. Defaults to null.</param>
    /// <param name="messageType">
    /// Specifies the type of the message, influencing the prefix and color. Defaults to <see cref="MessageType.Info"/>.
    /// </param>
    /// <param name="target">
    /// Specifies the target(s) where the message should be displayed. Defaults to <see cref="MessageTarget.All"/>.
    /// </param>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;

        // Handle debug messages separately - write to file instead of console
        if (messageType == MessageType.Debug)
        {
            var debugMessage = details != null ? $"{message} - {details}" : message;
            WriteDebugToFile(debugMessage);
            return;
        }

        var (prefix, color) = messageType switch
        {
            MessageType.Info => ("ℹ️ INFO", ConsoleColor.Cyan),
            MessageType.Warning => ("⚠️ WARNING", ConsoleColor.Yellow),
            MessageType.Error => ("❌ ERROR", ConsoleColor.Red),
            MessageType.Success => ("✅ SUCCESS", ConsoleColor.Green),
            MessageType.Critical => ("🚨 CRITICAL", ConsoleColor.Magenta),
            _ => ("INFO", ConsoleColor.Cyan)
        };

        WriteColoredMessage(prefix, message, color);

        if (details != null) WriteColoredMessage("   Details", details, ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Displays and manages a progress indicator for a task with a defined title and a total number of items.
    /// Returns a progress reporter instance to update the progress during execution.
    /// </summary>
    /// <param name="title">The title of the progress, displayed alongside the progress indicator.</param>
    /// <param name="totalItems">The total number of items or steps that represent the progress completion.</param>
    /// <returns>An <see cref="IProgress{T}"/> implementation, allowing updates to be sent during task progress.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new CliProgress(title, _useColors);
    }

    /// <summary>
    /// Creates a progress context for tracking and updating progress information
    /// with a specified title and total number of items.
    /// </summary>
    /// <param name="title">The title of the progress context, describing the operation being tracked.</param>
    /// <param name="totalItems">The total number of items to track progress for.</param>
    /// <returns>An instance of <see cref="IProgressContext"/> for managing and reporting progress.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new CliProgressContext(title, totalItems, _useColors);
    }

    /// <summary>
    /// Writes a message to the console with a specific color and prefix.
    /// Handles optional color formatting and timestamps. Works with active progress
    /// to ensure proper interruption and redraw if necessary.
    /// </summary>
    /// <param name="prefix">The prefix indicating the type of message (e.g., "ℹ️ INFO").</param>
    /// <param name="message">The main content of the message to be displayed.</param>
    /// <param name="color">The console color to apply to the prefix and message text.</param>
    private void WriteColoredMessage(string prefix, string message, ConsoleColor color)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");

        // If there's an active progress bar, interrupt it properly
        var needsProgressRedraw = ActiveProgress?.InterruptForMessage() ?? false;

        if (_useColors)
        {
            Console.Write($"[{timestamp}] ");
            Console.ForegroundColor = color;
            Console.Write($"{prefix}: ");
            Console.ResetColor();
            Console.WriteLine(message);
        }
        else
        {
            // Fallback for terminals that don't support colors
            var cleanPrefix = RemoveEmojis(prefix);
            Console.WriteLine($"[{timestamp}] {cleanPrefix}: {message}");
        }

        // Redraw the progress bar if needed
        if (needsProgressRedraw) ActiveProgress?.RedrawAfterMessage();
    }

    /// <summary>
    /// Removes all emoji characters from the given text string and trims any extra whitespace.
    /// </summary>
    /// <param name="text">The input string from which emojis will be removed.</param>
    /// <returns>A string with all emoji characters removed and any leading or trailing whitespace trimmed.</returns>
    private static string RemoveEmojis(string text)
    {
        return text
            .Replace("ℹ️", "")
            .Replace("⚠️", "")
            .Replace("❌", "")
            .Replace("✅", "")
            .Replace("🔍", "")
            .Replace("🚨", "")
            .Trim();
    }

    /// <summary>
    /// Determines whether the current environment supports colored console output.
    /// </summary>
    /// <returns>
    /// True if the console supports colored output; otherwise, false.
    /// </returns>
    private static bool SupportsColors()
    {
        try
        {
            // Check if we're in a console that supports colors
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // Modern Windows consoles support colors
                return Environment.OSVersion.Version.Major >= 10;

            // Unix-like systems generally support colors
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes a debug message to the current log file with a timestamped entry.
    /// This provides persistent storage for debugging output.
    /// </summary>
    /// <param name="message">The debug message to be written to the log file.</param>
    private void WriteDebugToFile(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] DEBUG: {message}{Environment.NewLine}";
            File.AppendAllText(_currentLogFile, logEntry);
        }
        catch
        {
            // Silently ignore file write errors to avoid disrupting the CLI
        }
    }

    /// <summary>
    /// Removes old debug log files from the log directory, keeping only the 10 most recent files
    /// to conserve storage and maintain log management efficiency.
    /// </summary>
    private void CleanupOldLogFiles()
    {
        try
        {
            var logFiles = Directory.GetFiles(_logDirectory, "scanner111-debug-*.log")
                .OrderByDescending(File.GetCreationTime)
                .ToArray();

            // Keep only the 10 most recent files
            for (var i = 10; i < logFiles.Length; i++)
                try
                {
                    File.Delete(logFiles[i]);
                }
                catch
                {
                    // Ignore deletion errors
                }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Implements a command-line interface (CLI) based progress tracker with a text-based progress bar.
/// Supports real-time updates and the ability to handle interruptions for messages.
/// </summary>
public class CliProgress : IProgress<ProgressInfo>
{
    private readonly bool _useColors;
    private ProgressInfo? _currentProgress;
    private bool _isActive;
    private int _lastPercentage = -1;

    public CliProgress(string title, bool useColors)
    {
        _useColors = useColors;

        Console.WriteLine($"\n{title}");
        _isActive = true;
        CliMessageHandler.ActiveProgress = this;
    }

    /// <summary>
    /// Reports the progress update for the current operation. It updates the progress bar
    /// and manages the visual display based on the provided progress information.
    /// </summary>
    /// <param name="value">The progress information containing the current progress, total items, and an optional message.</param>
    public void Report(ProgressInfo value)
    {
        if (!_isActive) return;

        _currentProgress = value;
        var percentage = (int)value.Percentage;

        // Only update if percentage changed to avoid spam
        if (percentage == _lastPercentage) return;
        _lastPercentage = percentage;

        DrawProgressBar(value);

        if (percentage < 100) return;
        Console.WriteLine(); // New line when complete
        _isActive = false;
        if (CliMessageHandler.ActiveProgress == this)
            CliMessageHandler.ActiveProgress = null;
    }

    /// <summary>
    /// Interrupts the CLI progress bar to allow a message to be displayed without overlapping.
    /// Ensures a proper visual transition between the progress bar and the message.
    /// </summary>
    /// <returns>
    /// Returns <c>true</c> if the interruption was successfully handled (e.g., a new line was added),
    /// or <c>false</c> if the progress bar was not active and no interruption was necessary.
    /// </returns>
    public bool InterruptForMessage()
    {
        if (!_isActive) return false;

        // Move to new line if we're currently showing progress on the same line
        Console.WriteLine();
        return true;
    }

    /// <summary>
    /// Redraws the progress bar to the console after an interruption caused by a message.
    /// This ensures the progress bar is displayed correctly and in sync with the current progress state.
    /// </summary>
    public void RedrawAfterMessage()
    {
        if (!_isActive || _currentProgress == null) return;

        // Redraw the progress bar on a new line
        DrawProgressBar(_currentProgress);
    }

    /// <summary>
    /// Renders a text-based progress bar to the console representing the state of a long-running operation.
    /// Optionally utilizes colored output for better visual distinction if enabled.
    /// </summary>
    /// <param name="value">The current progress information, including the percentage, message, and current/total values.</param>
    private void DrawProgressBar(ProgressInfo value)
    {
        var percentage = (int)value.Percentage;
        const int barWidth = 40;
        var filledWidth = (int)(barWidth * (percentage / 100.0));
        var bar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);

        if (_useColors)
        {
            Console.Write("\r[");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', filledWidth));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', barWidth - filledWidth));
            Console.ResetColor();
            Console.Write($"] {percentage}% - {value.Message}");
        }
        else
        {
            Console.Write($"\r[{bar}] {percentage}% - {value.Message}");
        }
    }
}

/// <summary>
/// Provides a CLI-specific implementation of a progress context, supporting progress updates
/// with detailed messages for long-running operations. Can be utilized with a 'using' statement
/// for proper disposal and resource management.
/// </summary>
public class CliProgressContext : IProgressContext
{
    private readonly CliProgress _progress;
    private readonly int _totalItems;
    private bool _disposed;

    public CliProgressContext(string title, int totalItems, bool useColors)
    {
        _totalItems = totalItems;
        _progress = new CliProgress(title, useColors);
    }

    /// <summary>
    /// Updates the progress information during a long-running operation.
    /// </summary>
    /// <param name="current">The current progress value, typically representing the number of items processed.</param>
    /// <param name="message">A message describing the current operation or status.</param>
    public void Update(int current, string message)
    {
        if (_disposed) return;

        var progressInfo = new ProgressInfo
        {
            Current = current,
            Total = _totalItems,
            Message = message
        };

        _progress.Report(progressInfo);
    }

    /// <summary>
    /// Marks the progress context as complete by updating the progress to the total items
    /// with a message indicating completion. This method should be called when the operation
    /// associated with the progress context finishes.
    /// </summary>
    /// <remarks>
    /// If the context has already been disposed, this method will have no effect.
    /// </remarks>
    public void Complete()
    {
        if (_disposed) return;

        Update(_totalItems, "Complete");
    }

    /// <summary>
    /// Reports the progress of the current operation by providing detailed progress information.
    /// Typically used for tracking and updating long-running tasks in a CLI environment.
    /// </summary>
    /// <param name="value">
    /// The progress information that contains the current progress count, total items to process,
    /// and an optional descriptive message to display alongside the progress bar.
    /// </param>
    public void Report(ProgressInfo value)
    {
        if (_disposed) return;
        _progress.Report(value);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="CliProgressContext"/> instance,
    /// ensuring proper cleanup of progress-related states and output.
    /// </summary>
    /// <remarks>
    /// This method concludes the progress context by performing necessary operations to finalize
    /// the progress display, resets the active progress reference if applicable,
    /// and ensures proper disposal of resources.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up active progress reference
        if (CliMessageHandler.ActiveProgress == _progress)
            CliMessageHandler.ActiveProgress = null;

        // Ensure we end on a new line
        Console.WriteLine();
        GC.SuppressFinalize(this);
    }
}