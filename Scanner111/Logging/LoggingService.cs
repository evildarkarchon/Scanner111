using System;
using Microsoft.Extensions.Logging;

namespace Scanner111.Logging;

/// <summary>
/// Default implementation of the unified logging service.
/// Uses Microsoft.Extensions.Logging under the hood.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingService"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public LoggingService(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<LoggingService>();
    }

    /// <inheritdoc />
    public void Log(LogLevel logLevel, string message)
    {
        _logger.Log(logLevel, message);
    }

    /// <inheritdoc />
    public void Log(LogLevel logLevel, Exception exception, string message)
    {
        _logger.Log(logLevel, exception, message);
    }

    /// <inheritdoc />
    public void Trace(string message)
    {
        _logger.LogTrace(message);
    }

    /// <inheritdoc />
    public void Debug(string message)
    {
        _logger.LogDebug(message);
    }

    /// <inheritdoc />
    public void Information(string message)
    {
        _logger.LogInformation(message);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        _logger.LogWarning(message);
    }

    /// <inheritdoc />
    public void Error(string message)
    {
        _logger.LogError(message);
    }

    /// <inheritdoc />
    public void Error(Exception exception, string message)
    {
        _logger.LogError(exception, message);
    }

    /// <inheritdoc />
    public void Critical(string message)
    {
        _logger.LogCritical(message);
    }

    /// <inheritdoc />
    public void Critical(Exception exception, string message)
    {
        _logger.LogCritical(exception, message);
    }

    /// <inheritdoc />
    public ILogger<T> GetLogger<T>() where T : class
    {
        return _loggerFactory.CreateLogger<T>();
    }
}