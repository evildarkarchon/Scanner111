using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Defines the contract for a messaging service that handles message notifications and progress reporting.
/// </summary>
public interface IMessageService : IAsyncDisposable
{
    /// <summary>
    ///     Occurs when a new message is published.
    /// </summary>
    event EventHandler<Message>? MessagePublished;

    /// <summary>
    ///     Sends an informational message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="details">Optional detailed information.</param>
    void Info(string content, string? title = null, string? details = null);

    /// <summary>
    ///     Sends a warning message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="details">Optional detailed information.</param>
    void Warning(string content, string? title = null, string? details = null);

    /// <summary>
    ///     Sends an error message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="exception">Optional exception associated with the error.</param>
    /// <param name="details">Optional detailed information.</param>
    void Error(string content, string? title = null, Exception? exception = null, string? details = null);

    /// <summary>
    ///     Sends a success message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="details">Optional detailed information.</param>
    void Success(string content, string? title = null, string? details = null);

    /// <summary>
    ///     Sends a debug message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="details">Optional detailed information.</param>
    void Debug(string content, string? title = null, string? details = null);

    /// <summary>
    ///     Sends a critical message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="title">Optional message title.</param>
    /// <param name="exception">Optional exception associated with the critical issue.</param>
    /// <param name="details">Optional detailed information.</param>
    void Critical(string content, string? title = null, Exception? exception = null, string? details = null);

    /// <summary>
    ///     Publishes a message.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    void Publish(Message message);

    /// <summary>
    ///     Creates a progress reporter for tracking operation progress.
    /// </summary>
    /// <param name="description">Description of the operation.</param>
    /// <param name="total">Optional total value for determinate progress.</param>
    /// <returns>An IProgress instance for reporting progress updates.</returns>
    IProgress<ProgressReport> CreateProgressReporter(string description, int? total = null);

    /// <summary>
    ///     Executes an operation with progress tracking.
    ///     Thread-safe for concurrent operations.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the operation.</typeparam>
    /// <param name="description">Description of the operation.</param>
    /// <param name="operation">The operation to execute with progress reporting.</param>
    /// <param name="total">Optional total value for determinate progress.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteWithProgressAsync<T>(
        string description,
        Func<IProgress<ProgressReport>, CancellationToken, Task<T>> operation,
        int? total = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes an operation with progress tracking.
    ///     Thread-safe for concurrent operations.
    /// </summary>
    /// <param name="description">Description of the operation.</param>
    /// <param name="operation">The operation to execute with progress reporting.</param>
    /// <param name="total">Optional total value for determinate progress.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ExecuteWithProgressAsync(
        string description,
        Func<IProgress<ProgressReport>, CancellationToken, Task> operation,
        int? total = null,
        CancellationToken cancellationToken = default);
}