namespace Scanner111.Core.Models;

/// <summary>
///     Defines the types of messages that can be sent through the messaging system.
/// </summary>
public enum MessageType
{
    /// <summary>
    ///     Informational message providing general updates.
    /// </summary>
    Info,

    /// <summary>
    ///     Warning message indicating potential issues.
    /// </summary>
    Warning,

    /// <summary>
    ///     Error message indicating a failure or problem.
    /// </summary>
    Error,

    /// <summary>
    ///     Success message indicating successful completion.
    /// </summary>
    Success,

    /// <summary>
    ///     Debug message for diagnostic purposes.
    /// </summary>
    Debug,

    /// <summary>
    ///     Critical message indicating severe issues requiring immediate attention.
    /// </summary>
    Critical
}