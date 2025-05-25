using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

namespace Scanner111.Logging;

/// <summary>
/// Provides configuration methods for the application's logging system.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures the application's logging services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);

            // Configure file logging
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logsDirectory)) Directory.CreateDirectory(logsDirectory);

            // Add our custom file logger
            var logFilePath = Path.Combine(logsDirectory, $"scanner111-{DateTime.Now:yyyy-MM-dd}.log");
            builder.AddFile(logFilePath, options =>
            {
                options.MinLevel = LogLevel.Information;
                options.FileSizeLimitBytes = 10 * 1024 * 1024; // 10MB
                options.RetainedFileCountLimit = 10;
            });

            // Add debug logger provider manually instead of using AddDebug extension
            builder.AddProvider(new DebugLoggerProvider());
        });

        // Register our logging service
        services.AddSingleton<ILoggingService, LoggingService>();

        return services;
    }
}