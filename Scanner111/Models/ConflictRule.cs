namespace Scanner111.Models;

/// <summary>
///     Represents a conflict rule between two plugins
/// </summary>
public class ConflictRule
{
    /// <summary>
    ///     Gets or sets the first plugin name
    /// </summary>
    public string PluginA { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the second plugin name
    /// </summary>
    public string PluginB { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the conflict message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the conflict title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the recommendation for resolving the conflict
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the severity level of the conflict
    /// </summary>
    public SeverityLevel Severity { get; set; } = SeverityLevel.Warning;
}