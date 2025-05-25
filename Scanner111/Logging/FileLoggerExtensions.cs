using System;
using Microsoft.Extensions.Logging;

namespace Scanner111.Logging;

/// <summary>
/// Extension methods for adding file logging capability.
/// </summary>
public static class FileLoggerExtensions
{
    /// <summary>
    /// Adds a file logger to the factory with default settings.
    /// </summary>
    /// <param name="builder">The ILoggingBuilder to add the file logger to.</param>
    /// <param name="filePath">The file path to log to.</param>
    /// <returns>The ILoggingBuilder for chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        return builder.AddFile(filePath, LogLevel.Information);
    }

    /// <summary>
    /// Adds a file logger to the factory with custom settings.
    /// </summary>
    /// <param name="builder">The ILoggingBuilder to add the file logger to.</param>
    /// <param name="filePath">The file path to log to.</param>
    /// <param name="minLevel">The minimum log level to capture.</param>
    /// <returns>The ILoggingBuilder for chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath, LogLevel minLevel)
    {
        builder.AddProvider(new FileLoggerProvider(filePath, minLevel));
        return builder;
    }

    /// <summary>
    /// Adds a file logger to the factory with custom settings.
    /// </summary>
    /// <param name="builder">The ILoggingBuilder to add the file logger to.</param>
    /// <param name="filePath">The file path to log to.</param>
    /// <param name="configure">A callback to configure the file logger options.</param>
    /// <returns>The ILoggingBuilder for chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath,
        Action<FileLoggerOptions> configure)
    {
        var options = new FileLoggerOptions();
        configure(options);

        builder.AddProvider(new FileLoggerProvider(filePath, options.MinLevel));
        return builder;
    }

    /// <summary>
    /// Options for configuring the file logger.
    /// </summary>
    public class FileLoggerOptions
    {
        /// <summary>
        /// Gets or sets the minimum log level.
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets the maximum file size in bytes before rolling.
        /// </summary>
        public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default

        /// <summary>
        /// Gets or sets the maximum number of log files to keep.
        /// </summary>
        public int RetainedFileCountLimit { get; set; } = 5;
    }
}