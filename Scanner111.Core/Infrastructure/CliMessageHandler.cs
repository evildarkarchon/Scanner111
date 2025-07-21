using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Console-based message handler with colored output (matches Python CLI implementation)
/// </summary>
public class CliMessageHandler : IMessageHandler
{
    private readonly bool _useColors;
    private readonly string _logDirectory;
    private readonly string _currentLogFile;
    internal static CliProgress? _activeProgress;

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

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("ℹ️ INFO", message, ConsoleColor.Cyan);
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("⚠️ WARNING", message, ConsoleColor.Yellow);
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("❌ ERROR", message, ConsoleColor.Red);
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("✅ SUCCESS", message, ConsoleColor.Green);
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteDebugToFile(message);
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("🚨 CRITICAL", message, ConsoleColor.Magenta);
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info, MessageTarget target = MessageTarget.All)
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
        
        if (details != null)
        {
            WriteColoredMessage("   Details", details, ConsoleColor.DarkGray);
        }
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new CliProgress(title, totalItems, _useColors);
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new CliProgressContext(title, totalItems, _useColors);
    }

    private void WriteColoredMessage(string prefix, string message, ConsoleColor color)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        
        // If there's an active progress bar, interrupt it properly
        var needsProgressRedraw = _activeProgress?.InterruptForMessage() ?? false;
        
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
        if (needsProgressRedraw)
        {
            _activeProgress?.RedrawAfterMessage();
        }
    }

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

    private static bool SupportsColors()
    {
        try
        {
            // Check if we're in a console that supports colors
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Modern Windows consoles support colors
                return Environment.OSVersion.Version.Major >= 10;
            }
            
            // Unix-like systems generally support colors
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));
        }
        catch
        {
            return false;
        }
    }

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

    private void CleanupOldLogFiles()
    {
        try
        {
            var logFiles = Directory.GetFiles(_logDirectory, "scanner111-debug-*.log")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();

            // Keep only the 10 most recent files
            for (int i = 10; i < logFiles.Length; i++)
            {
                try
                {
                    File.Delete(logFiles[i]);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// CLI progress implementation with text-based progress bar
/// </summary>
public class CliProgress : IProgress<ProgressInfo>
{
    private readonly string _title;
    private readonly int _totalItems;
    private readonly bool _useColors;
    private int _lastPercentage = -1;
    private ProgressInfo? _currentProgress;
    private bool _isActive;

    public CliProgress(string title, int totalItems, bool useColors)
    {
        _title = title;
        _totalItems = totalItems;
        _useColors = useColors;
        
        Console.WriteLine($"\n{_title}");
        _isActive = true;
        CliMessageHandler._activeProgress = this;
    }

    public void Report(ProgressInfo value)
    {
        if (!_isActive) return;
        
        _currentProgress = value;
        var percentage = (int)value.Percentage;
        
        // Only update if percentage changed to avoid spam
        if (percentage == _lastPercentage) return;
        _lastPercentage = percentage;

        DrawProgressBar(value);
        
        if (percentage >= 100)
        {
            Console.WriteLine(); // New line when complete
            _isActive = false;
            if (CliMessageHandler._activeProgress == this)
                CliMessageHandler._activeProgress = null;
        }
    }

    public bool InterruptForMessage()
    {
        if (!_isActive) return false;
        
        // Move to new line if we're currently showing progress on the same line
        Console.WriteLine();
        return true;
    }

    public void RedrawAfterMessage()
    {
        if (!_isActive || _currentProgress == null) return;
        
        // Redraw the progress bar on a new line
        DrawProgressBar(_currentProgress);
    }

    private void DrawProgressBar(ProgressInfo value)
    {
        var percentage = (int)value.Percentage;
        var barWidth = 40;
        var filledWidth = (int)(barWidth * (percentage / 100.0));
        var bar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);
        
        if (_useColors)
        {
            Console.Write($"\r[");
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
/// CLI progress context with 'using' statement support
/// </summary>
public class CliProgressContext : IProgressContext
{
    private readonly CliProgress _progress;
    private readonly int _totalItems;
    private bool _disposed;

    public CliProgressContext(string title, int totalItems, bool useColors)
    {
        _totalItems = totalItems;
        _progress = new CliProgress(title, totalItems, useColors);
    }

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

    public void Complete()
    {
        if (_disposed) return;
        
        Update(_totalItems, "Complete");
    }

    public void Report(ProgressInfo value)
    {
        if (_disposed) return;
        _progress.Report(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Clean up active progress reference
        if (CliMessageHandler._activeProgress == _progress)
            CliMessageHandler._activeProgress = null;
        
        // Ensure we end on a new line
        Console.WriteLine();
    }
}