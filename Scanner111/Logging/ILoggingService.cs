using System;
using Microsoft.Extensions.Logging;

namespace Scanner111.Logging;

/// <summary>
/// Interface for the application's unified logging service.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs a message at the specified log level.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="message">The log message.</param>
    void Log(LogLevel logLevel, string message);

    /// <summary>
    /// Logs a message at the specified log level with an exception.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">The log message.</param>
    void Log(LogLevel logLevel, Exception exception, string message);

    /// <summary>
    /// Logs a trace message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Trace(string message);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Debug(string message);

    /// <summary>
    /// Logs an information message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Information(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Warning(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Error(string message);

    /// <summary>
    /// Logs an error message with an exception.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">The log message.</param>
    void Error(Exception exception, string message);

    /// <summary>
    /// Logs a critical message.
    /// </summary>
    /// <param name="message">The log message.</param>
    void Critical(string message);

    /// <summary>
    /// Logs a critical message with an exception.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">The log message.</param>
    void Critical(Exception exception, string message);

    /// <summary>
    /// Gets an instance of ILogger<T> for the specified type.
    /// This allows for direct access to the underlying logging system when needed.
    /// </summary>
    /// <typeparam name="T">The category type.</typeparam>
    /// <returns>An instance of ILogger<T>.</returns>
    ILogger<T> GetLogger<T>() where T : class;
}