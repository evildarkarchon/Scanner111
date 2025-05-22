namespace Scanner111.Models;

/// <summary>
///     Contains details about a warning, error, or information message
/// </summary>
public class WarningDetails
{
    /// <summary>
    ///     Gets or sets the title or subject of the warning
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the detailed message explaining the warning
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the severity level of this warning
    /// </summary>
    public SeverityLevel Severity { get; set; } = SeverityLevel.Information;

    /// <summary>
    ///     Gets or sets a recommended action to address the warning
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
}