namespace Scanner111.Core.Models;

/// <summary>
/// Represents the result of an update check operation
/// </summary>
public class UpdateCheckResult
{
    /// <summary>
    /// Current installed version
    /// </summary>
    public Version? CurrentVersion { get; set; }
    
    /// <summary>
    /// Latest version available from GitHub
    /// </summary>
    public Version? LatestGitHubVersion { get; set; }
    
    /// <summary>
    /// Latest version available from Nexus
    /// </summary>
    public Version? LatestNexusVersion { get; set; }
    
    /// <summary>
    /// Whether an update is available
    /// </summary>
    public bool IsUpdateAvailable { get; set; }
    
    /// <summary>
    /// Whether the update check was successful
    /// </summary>
    public bool CheckSuccessful { get; set; }
    
    /// <summary>
    /// Error message if check failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Source that provided the latest version (GitHub, Nexus, or Both)
    /// </summary>
    public string UpdateSource { get; set; } = "Both";
}

/// <summary>
/// Exception thrown when update checking fails
/// </summary>
public class UpdateCheckException : Exception
{
    public UpdateCheckException(string message) : base(message) { }
    public UpdateCheckException(string message, Exception innerException) : base(message, innerException) { }
}