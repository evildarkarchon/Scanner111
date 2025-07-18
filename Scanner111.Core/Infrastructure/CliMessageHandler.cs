using System;
using System.Runtime.InteropServices;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Console-based message handler with colored output (matches Python CLI implementation)
/// </summary>
public class CliMessageHandler : IMessageHandler
{
    private readonly bool _useColors;

    public CliMessageHandler(bool useColors = true)
    {
        _useColors = useColors && SupportsColors();
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
        WriteColoredMessage("🔍 DEBUG", message, ConsoleColor.Gray);
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        WriteColoredMessage("🚨 CRITICAL", message, ConsoleColor.Magenta);
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;

        var (prefix, color) = messageType switch
        {
            MessageType.Info => ("ℹ️ INFO", ConsoleColor.Cyan),
            MessageType.Warning => ("⚠️ WARNING", ConsoleColor.Yellow),
            MessageType.Error => ("❌ ERROR", ConsoleColor.Red),
            MessageType.Success => ("✅ SUCCESS", ConsoleColor.Green),
            MessageType.Debug => ("🔍 DEBUG", ConsoleColor.Gray),
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

    public CliProgress(string title, int totalItems, bool useColors)
    {
        _title = title;
        _totalItems = totalItems;
        _useColors = useColors;
        
        Console.WriteLine($"\n{_title}");
    }

    public void Report(ProgressInfo value)
    {
        var percentage = (int)value.Percentage;
        
        // Only update if percentage changed to avoid spam
        if (percentage == _lastPercentage) return;
        _lastPercentage = percentage;

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
        
        if (percentage >= 100)
        {
            Console.WriteLine(); // New line when complete
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
        
        // Ensure we end on a new line
        Console.WriteLine();
    }
}