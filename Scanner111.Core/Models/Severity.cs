namespace Scanner111.Core.Models;

/// <summary>
/// Represents the severity level of an analysis result.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info = 0,
    
    /// <summary>
    /// Warning that should be addressed.
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Error that needs attention.
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Critical issue that must be resolved.
    /// </summary>
    Critical = 3
}