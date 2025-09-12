using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Configuration;

/// <summary>
/// Application-wide settings data structure.
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// Gets or sets the default game type.
    /// </summary>
    public GameType DefaultGame { get; set; } = GameType.Fallout4;
    
    /// <summary>
    /// Gets or sets whether to auto-detect game paths.
    /// </summary>
    public bool AutoDetectPaths { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of parallel analyzers.
    /// </summary>
    public int MaxParallelAnalyzers { get; set; } = 4;
    
    /// <summary>
    /// Gets or sets the default report format.
    /// </summary>
    public ReportFormat DefaultReportFormat { get; set; } = ReportFormat.Markdown;
    
    /// <summary>
    /// Gets or sets the UI theme.
    /// </summary>
    public string Theme { get; set; } = "Default";
    
    /// <summary>
    /// Gets or sets whether to show timestamps in output.
    /// </summary>
    public bool ShowTimestamps { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable verbose output.
    /// </summary>
    public bool VerboseOutput { get; set; }
    
    /// <summary>
    /// Gets or sets the custom log directory.
    /// </summary>
    public string? LogDirectory { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable debug mode.
    /// </summary>
    public bool DebugMode { get; set; }
    
    /// <summary>
    /// Gets or sets the analysis timeout in seconds.
    /// </summary>
    public int AnalysisTimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// Gets or sets whether to cache analysis results.
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the cache expiration in minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Gets or sets whether FCX Mode analysis is enabled.
    /// </summary>
    public bool EnableFcxModeAnalysis { get; set; } = true;
    
    /// <summary>
    /// Gets or sets custom game paths.
    /// </summary>
    public Dictionary<string, string> CustomGamePaths { get; set; } = new();
    
    /// <summary>
    /// Gets or sets additional settings for extensibility.
    /// </summary>
    public Dictionary<string, object> ExtendedSettings { get; set; } = new();
}