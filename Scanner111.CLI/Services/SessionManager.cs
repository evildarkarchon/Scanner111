using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Extensions;
using Scanner111.Core.Analysis;
using System.Collections.Concurrent;
using System.Text;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for managing analysis sessions with persistence and comparison capabilities.
/// </summary>
public class SessionManager : ISessionManager, IDisposable
{
    private readonly string _sessionPath;
    private readonly ILogger<SessionManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<Guid, Session> _sessionCache = new();
    private readonly Timer _autoSaveTimer;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager"/> class.
    /// </summary>
    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _sessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "Sessions");
        
        Directory.CreateDirectory(_sessionPath);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Setup auto-save timer (every 30 seconds)
        _autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
    
    /// <summary>
    /// Creates a new analysis session.
    /// </summary>
    public async Task<Session> CreateSessionAsync(string logFile, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                LogFile = logFile,
                StartTime = DateTime.UtcNow,
                Results = new List<AnalysisResult>()
            };
            
            await SaveSessionAsync(session, cancellationToken);
            _sessionCache[session.Id] = session;
            _logger.LogInformation("Created new session {SessionId} for {LogFile}", session.Id, logFile);
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for {LogFile}", logFile);
            throw;
        }
    }
    
    /// <summary>
    /// Updates a session with analysis results.
    /// </summary>
    public async Task UpdateSessionAsync(
        Guid sessionId, 
        IEnumerable<AnalysisResult> results, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await LoadSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }
            
            session.Results = results.ToList();
            session.EndTime = DateTime.UtcNow;
            session.Duration = session.EndTime - session.StartTime;
            
            _sessionCache[sessionId] = session;
            await SaveSessionAsync(session, cancellationToken);
            _logger.LogInformation("Updated session {SessionId} with {Count} results", sessionId, session.Results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session {SessionId}", sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// Gets recent sessions.
    /// </summary>
    public async Task<IEnumerable<Session>> GetRecentSessionsAsync(
        int count = 10, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_sessionPath))
            {
                return Enumerable.Empty<Session>();
            }
            
            var sessionFiles = Directory.GetFiles(_sessionPath, "*.session")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(count);
            
            var sessions = new List<Session>();
            foreach (var file in sessionFiles)
            {
                try
                {
                    var session = await LoadSessionFromFileAsync(file, cancellationToken);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session from {File}", file);
                }
            }
            
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent sessions");
            return Enumerable.Empty<Session>();
        }
    }
    
    /// <summary>
    /// Loads a specific session.
    /// </summary>
    public async Task<Session?> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetSessionFilePath(sessionId);
            if (!File.Exists(filePath))
            {
                return null;
            }
            
            return await LoadSessionFromFileAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {SessionId}", sessionId);
            return null;
        }
    }
    
    private async Task SaveSessionAsync(Session session, CancellationToken cancellationToken)
    {
        var filePath = GetSessionFilePath(session.Id);
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    private async Task<Session?> LoadSessionFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<Session>(json, _jsonOptions);
    }
    
    private string GetSessionFilePath(Guid sessionId)
    {
        return Path.Combine(_sessionPath, $"{sessionId}.session");
    }
    
    /// <summary>
    /// Compares two sessions and returns a comparison report.
    /// </summary>
    public async Task<SessionComparison> CompareSessionsAsync(
        Guid sessionId1, 
        Guid sessionId2, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session1 = await LoadSessionAsync(sessionId1, cancellationToken);
            var session2 = await LoadSessionAsync(sessionId2, cancellationToken);
            
            if (session1 == null)
                throw new ArgumentException($"Session {sessionId1} not found", nameof(sessionId1));
            if (session2 == null)
                throw new ArgumentException($"Session {sessionId2} not found", nameof(sessionId2));
            
            return CreateSessionComparison(session1, session2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare sessions {SessionId1} and {SessionId2}", sessionId1, sessionId2);
            throw;
        }
    }
    
    /// <summary>
    /// Exports a session to a specified file path.
    /// </summary>
    public async Task ExportSessionAsync(
        Guid sessionId, 
        string exportPath, 
        SessionExportFormat format = SessionExportFormat.Json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await LoadSessionAsync(sessionId, cancellationToken);
            if (session == null)
                throw new ArgumentException($"Session {sessionId} not found", nameof(sessionId));
            
            switch (format)
            {
                case SessionExportFormat.Json:
                    await ExportAsJsonAsync(session, exportPath, cancellationToken);
                    break;
                case SessionExportFormat.Csv:
                    await ExportAsCsvAsync(session, exportPath, cancellationToken);
                    break;
                case SessionExportFormat.Html:
                    await ExportAsHtmlAsync(session, exportPath, cancellationToken);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
            }
            
            _logger.LogInformation("Exported session {SessionId} to {ExportPath} as {Format}", sessionId, exportPath, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export session {SessionId} to {ExportPath}", sessionId, exportPath);
            throw;
        }
    }
    
    /// <summary>
    /// Imports a session from a file.
    /// </summary>
    public async Task<Session> ImportSessionAsync(
        string importPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(importPath))
                throw new FileNotFoundException($"Import file not found: {importPath}");
            
            var json = await File.ReadAllTextAsync(importPath, cancellationToken);
            var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);
            
            if (session == null)
                throw new InvalidOperationException("Failed to deserialize session from import file");
            
            // Generate new ID to avoid conflicts
            session.Id = Guid.NewGuid();
            
            await SaveSessionAsync(session, cancellationToken);
            _sessionCache[session.Id] = session;
            
            _logger.LogInformation("Imported session {SessionId} from {ImportPath}", session.Id, importPath);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import session from {ImportPath}", importPath);
            throw;
        }
    }
    
    /// <summary>
    /// Deletes old sessions based on age or count limits.
    /// </summary>
    public async Task CleanupOldSessionsAsync(
        TimeSpan? maxAge = null,
        int? maxCount = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessions = await GetAllSessionsAsync(cancellationToken);
            var sessionsToDelete = new List<Session>();
            
            // Apply age filter
            if (maxAge.HasValue)
            {
                var cutoffDate = DateTime.UtcNow - maxAge.Value;
                sessionsToDelete.AddRange(sessions.Where(s => s.StartTime < cutoffDate));
            }
            
            // Apply count filter
            if (maxCount.HasValue)
            {
                var excessSessions = sessions
                    .OrderByDescending(s => s.StartTime)
                    .Skip(maxCount.Value);
                sessionsToDelete.AddRange(excessSessions);
            }
            
            // Remove duplicates
            sessionsToDelete = sessionsToDelete.Distinct().ToList();
            
            foreach (var session in sessionsToDelete)
            {
                await DeleteSessionAsync(session.Id, cancellationToken);
            }
            
            _logger.LogInformation("Cleaned up {Count} old sessions", sessionsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old sessions");
            throw;
        }
    }
    
    /// <summary>
    /// Deletes a specific session.
    /// </summary>
    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask;
            
            var filePath = GetSessionFilePath(sessionId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _sessionCache.TryRemove(sessionId, out _);
                _logger.LogInformation("Deleted session {SessionId}", sessionId);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return false;
        }
    }
    
    /// <summary>
    /// Gets all sessions without limiting count.
    /// </summary>
    public async Task<IEnumerable<Session>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await GetRecentSessionsAsync(int.MaxValue, cancellationToken);
    }
    
    /// <summary>
    /// Gets session metadata without loading full results.
    /// </summary>
    public async Task<IEnumerable<SessionMetadata>> GetSessionMetadataAsync(
        int count = 50, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_sessionPath))
                return Enumerable.Empty<SessionMetadata>();
            
            var sessionFiles = Directory.GetFiles(_sessionPath, "*.session")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(count);
            
            var metadata = new List<SessionMetadata>();
            foreach (var file in sessionFiles)
            {
                try
                {
                    var session = await LoadSessionFromFileAsync(file, cancellationToken);
                    if (session != null)
                    {
                        metadata.Add(new SessionMetadata
                        {
                            Id = session.Id,
                            LogFile = session.LogFile,
                            StartTime = session.StartTime,
                            EndTime = session.EndTime,
                            Duration = session.Duration,
                            ResultCount = session.Results?.Count ?? 0,
                            FileSize = new FileInfo(file).Length
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load metadata from {File}", file);
                }
            }
            
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session metadata");
            return Enumerable.Empty<SessionMetadata>();
        }
    }
    
    private SessionComparison CreateSessionComparison(Session session1, Session session2)
    {
        var comparison = new SessionComparison
        {
            Session1Id = session1.Id,
            Session2Id = session2.Id,
            Session1StartTime = session1.StartTime,
            Session2StartTime = session2.StartTime,
            Session1ResultCount = session1.Results?.Count ?? 0,
            Session2ResultCount = session2.Results?.Count ?? 0,
            Session1Duration = session1.Duration,
            Session2Duration = session2.Duration
        };
        
        // Compare results by severity
        var results1 = session1.Results ?? new List<AnalysisResult>();
        var results2 = session2.Results ?? new List<AnalysisResult>();
        
        comparison.SeverityComparison = CompareSeverityDistribution(results1, results2);
        comparison.NewIssues = FindNewIssues(results1, results2);
        comparison.ResolvedIssues = FindNewIssues(results2, results1); // Reverse to find resolved
        comparison.CommonIssues = FindCommonIssues(results1, results2);
        
        return comparison;
    }
    
    private Dictionary<string, (int Session1Count, int Session2Count)> CompareSeverityDistribution(
        IEnumerable<AnalysisResult> results1, 
        IEnumerable<AnalysisResult> results2)
    {
        var severity1 = results1.GroupBy(r => r.Severity.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        var severity2 = results2.GroupBy(r => r.Severity.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        
        var allSeverities = severity1.Keys.Union(severity2.Keys);
        
        return allSeverities.ToDictionary(
            severity => severity,
            severity => (
                severity1.GetValueOrDefault(severity, 0),
                severity2.GetValueOrDefault(severity, 0)
            ));
    }
    
    private List<AnalysisResult> FindNewIssues(IEnumerable<AnalysisResult> current, IEnumerable<AnalysisResult> previous)
    {
        var previousTitles = new HashSet<string>(previous.Select(r => r.GetTitle() ?? ""));
        return current.Where(r => !previousTitles.Contains(r.GetTitle() ?? "")).ToList();
    }
    
    private List<AnalysisResult> FindCommonIssues(IEnumerable<AnalysisResult> results1, IEnumerable<AnalysisResult> results2)
    {
        var titles2 = new HashSet<string>(results2.Select(r => r.GetTitle() ?? ""));
        return results1.Where(r => titles2.Contains(r.GetTitle() ?? "")).ToList();
    }
    
    private async Task ExportAsJsonAsync(Session session, string exportPath, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(exportPath, json, cancellationToken);
    }
    
    private async Task ExportAsCsvAsync(Session session, string exportPath, CancellationToken cancellationToken)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Severity,Title,Summary,Analyzer");
        
        foreach (var result in session.Results ?? new List<AnalysisResult>())
        {
            csv.AppendLine($"{session.StartTime:yyyy-MM-dd HH:mm:ss}," +
                          $"{EscapeCsv(result.Severity.ToString() ?? "")}," +
                          $"{EscapeCsv(result.GetTitle() ?? "")}," +
                          $"{EscapeCsv(result.Fragment?.GetSummary() ?? "")}," +
                          $"{EscapeCsv(result.AnalyzerName ?? "")}");
        }
        
        await File.WriteAllTextAsync(exportPath, csv.ToString(), cancellationToken);
    }
    
    private async Task ExportAsHtmlAsync(Session session, string exportPath, CancellationToken cancellationToken)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Scanner111 Session Report</title>");
        html.AppendLine("<style>body{font-family:Arial,sans-serif}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background-color:#f2f2f2}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine($"<h1>Scanner111 Session Report</h1>");
        html.AppendLine($"<p><strong>Session ID:</strong> {session.Id}</p>");
        html.AppendLine($"<p><strong>Log File:</strong> {session.LogFile}</p>");
        html.AppendLine($"<p><strong>Start Time:</strong> {session.StartTime:yyyy-MM-dd HH:mm:ss}</p>");
        if (session.EndTime.HasValue)
            html.AppendLine($"<p><strong>End Time:</strong> {session.EndTime.Value:yyyy-MM-dd HH:mm:ss}</p>");
        if (session.Duration.HasValue)
            html.AppendLine($"<p><strong>Duration:</strong> {session.Duration.Value:hh\\:mm\\:ss}</p>");
        
        html.AppendLine("<h2>Analysis Results</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Severity</th><th>Title</th><th>Summary</th><th>Analyzer</th></tr>");
        
        foreach (var result in session.Results ?? new List<AnalysisResult>())
        {
            html.AppendLine($"<tr>" +
                           $"<td>{System.Web.HttpUtility.HtmlEncode(result.Severity.ToString() ?? "")}</td>" +
                           $"<td>{System.Web.HttpUtility.HtmlEncode(result.GetTitle() ?? "")}</td>" +
                           $"<td>{System.Web.HttpUtility.HtmlEncode(result.Fragment?.GetSummary() ?? "")}</td>" +
                           $"<td>{System.Web.HttpUtility.HtmlEncode(result.AnalyzerName ?? "")}</td>" +
                           $"</tr>");
        }
        
        html.AppendLine("</table></body></html>");
        
        await File.WriteAllTextAsync(exportPath, html.ToString(), cancellationToken);
    }
    
    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\""; // Escape quotes by doubling them
        }
        return value;
    }
    
    private async void AutoSaveCallback(object? state)
    {
        try
        {
            var sessionsToSave = _sessionCache.Values.Where(s => s.EndTime == null).ToList();
            foreach (var session in sessionsToSave)
            {
                await SaveSessionAsync(session, CancellationToken.None);
            }
            
            if (sessionsToSave.Any())
            {
                _logger.LogDebug("Auto-saved {Count} active sessions", sessionsToSave.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-save");
        }
    }
    
    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
    }
}