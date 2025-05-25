using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Logging;

/// <summary>
/// File logger provider implementation.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly LogLevel _minLevel;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggerProvider"/> class.
    /// </summary>
    /// <param name="path">Path to the log file.</param>
    /// <param name="minLevel">Minimum log level.</param>
    public FileLoggerProvider(string path, LogLevel minLevel = LogLevel.Information)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _minLevel = minLevel;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _path, _minLevel));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
        GC.SuppressFinalize(this);
    }

    private class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;
        private readonly string _path;

        public FileLogger(string categoryName, string path, LogLevel minLevel)
        {
            _categoryName = categoryName;
            _path = path;
            _minLevel = minLevel;
        }

        // Use explicit interface implementation to fix nullability constraint mismatch
        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return default!;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            try
            {
                var message = formatter(state, exception);
                var logEntry = BuildLogEntry(logLevel, _categoryName, message, exception);

                // Use locking to ensure thread safety
                lock (typeof(FileLogger))
                {
                    File.AppendAllText(_path, logEntry);
                }
            }
            catch
            {
                // Suppress any exceptions from logging
            }
        }

        private static string BuildLogEntry(
            LogLevel logLevel,
            string category,
            string message,
            Exception? exception)
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff "));
            builder.Append(GetLogLevelString(logLevel).PadRight(12));
            builder.Append($"[{category}] ");
            builder.Append($"{message}");

            if (exception != null) builder.Append($"{Environment.NewLine}{exception}");

            builder.Append(Environment.NewLine);
            return builder.ToString();
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                LogLevel.None => "NONE",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }
    }
}