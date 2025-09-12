using Scanner111.Core.Analysis;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for managing analysis sessions.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Creates a new analysis session.
    /// </summary>
    /// <param name="logFile">The log file being analyzed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created session.</returns>
    Task<Session> CreateSessionAsync(string logFile, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a session with analysis results.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="results">The analysis results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSessionAsync(Guid sessionId, IEnumerable<AnalysisResult> results, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets recent sessions.
    /// </summary>
    /// <param name="count">Number of sessions to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent sessions.</returns>
    Task<IEnumerable<Session>> GetRecentSessionsAsync(int count = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session, or null if not found.</returns>
    Task<Session?> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compares two sessions and returns a comparison report.
    /// </summary>
    /// <param name="sessionId1">First session ID.</param>
    /// <param name="sessionId2">Second session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session comparison result.</returns>
    Task<SessionComparison> CompareSessionsAsync(Guid sessionId1, Guid sessionId2, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a session to a file.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="exportPath">The export file path.</param>
    /// <param name="format">Export format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task ExportSessionAsync(Guid sessionId, string exportPath, SessionExportFormat format = SessionExportFormat.Json, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports a session from a file.
    /// </summary>
    /// <param name="importPath">The import file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported session.</returns>
    Task<Session> ImportSessionAsync(string importPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes old sessions based on criteria.
    /// </summary>
    /// <param name="maxAge">Maximum age of sessions to keep.</param>
    /// <param name="maxCount">Maximum number of sessions to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task CleanupOldSessionsAsync(TimeSpan? maxAge = null, int? maxCount = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets session metadata without loading full results.
    /// </summary>
    /// <param name="count">Number of sessions to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session metadata.</returns>
    Task<IEnumerable<SessionMetadata>> GetSessionMetadataAsync(int count = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an analysis session.
/// </summary>
public class Session
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets the log file path.
    /// </summary>
    public string LogFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the session end time.
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Gets or sets the analysis results.
    /// </summary>
    public List<AnalysisResult> Results { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the session duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Represents session metadata without full analysis results.
/// </summary>
public class SessionMetadata
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets the log file path.
    /// </summary>
    public string LogFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the session end time.
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Gets or sets the session duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }
    
    /// <summary>
    /// Gets or sets the number of analysis results.
    /// </summary>
    public int ResultCount { get; set; }
    
    /// <summary>
    /// Gets or sets the session file size in bytes.
    /// </summary>
    public long FileSize { get; set; }
}

/// <summary>
/// Represents a comparison between two sessions.
/// </summary>
public class SessionComparison
{
    /// <summary>
    /// Gets or sets the first session ID.
    /// </summary>
    public Guid Session1Id { get; set; }
    
    /// <summary>
    /// Gets or sets the second session ID.
    /// </summary>
    public Guid Session2Id { get; set; }
    
    /// <summary>
    /// Gets or sets the first session start time.
    /// </summary>
    public DateTime Session1StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the second session start time.
    /// </summary>
    public DateTime Session2StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the first session result count.
    /// </summary>
    public int Session1ResultCount { get; set; }
    
    /// <summary>
    /// Gets or sets the second session result count.
    /// </summary>
    public int Session2ResultCount { get; set; }
    
    /// <summary>
    /// Gets or sets the first session duration.
    /// </summary>
    public TimeSpan? Session1Duration { get; set; }
    
    /// <summary>
    /// Gets or sets the second session duration.
    /// </summary>
    public TimeSpan? Session2Duration { get; set; }
    
    /// <summary>
    /// Gets or sets the severity comparison data.
    /// </summary>
    public Dictionary<string, (int Session1Count, int Session2Count)> SeverityComparison { get; set; } = new();
    
    /// <summary>
    /// Gets or sets issues that are new in session 1.
    /// </summary>
    public List<AnalysisResult> NewIssues { get; set; } = new();
    
    /// <summary>
    /// Gets or sets issues that were resolved from session 2 to session 1.
    /// </summary>
    public List<AnalysisResult> ResolvedIssues { get; set; } = new();
    
    /// <summary>
    /// Gets or sets issues that are common to both sessions.
    /// </summary>
    public List<AnalysisResult> CommonIssues { get; set; } = new();
}

/// <summary>
/// Export format for sessions.
/// </summary>
public enum SessionExportFormat
{
    /// <summary>
    /// JSON format.
    /// </summary>
    Json,
    
    /// <summary>
    /// CSV format.
    /// </summary>
    Csv,
    
    /// <summary>
    /// HTML format.
    /// </summary>
    Html
}