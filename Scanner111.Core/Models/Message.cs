namespace Scanner111.Core.Models;

/// <summary>
///     Represents an immutable message in the messaging system.
/// </summary>
public record Message
{
    /// <summary>
    ///     Gets the content of the message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    ///     Gets the type of the message.
    /// </summary>
    public required MessageType Type { get; init; }

    /// <summary>
    ///     Gets the optional title for the message.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     Gets the optional detailed information about the message.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    ///     Gets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Gets the optional source/context of the message.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    ///     Gets any exception associated with the message.
    /// </summary>
    public Exception? Exception { get; init; }
}