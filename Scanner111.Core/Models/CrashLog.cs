namespace Scanner111.Core.Models;

/// <summary>
/// Represents a parsed crash log from Bethesda games using Buffout 4/Crash Logger format
/// </summary>
public class CrashLog
{
    /// <summary>
    /// Full path to the crash log file
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
    
    /// <summary>
    /// Just the filename without path
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);
    
    /// <summary>
    /// Original lines from the crash log file
    /// </summary>
    public List<string> OriginalLines { get; init; } = new();
    
    /// <summary>
    /// Full content as a single string
    /// </summary>
    public string Content => string.Join("\n", OriginalLines);
    
    // Parsed sections
    /// <summary>
    /// Main error message extracted from the crash log
    /// </summary>
    public string MainError { get; set; } = string.Empty;
    
    /// <summary>
    /// Call stack lines from the crash
    /// </summary>
    public List<string> CallStack { get; set; } = new();
    
    /// <summary>
    /// Plugin information: filename -> loadOrder
    /// </summary>
    public Dictionary<string, string> Plugins { get; set; } = new();
    
    /// <summary>
    /// Version of the crash generator (e.g., Buffout 4 version)
    /// </summary>
    public string CrashGenVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the crash occurred
    /// </summary>
    public DateTime? CrashTime { get; set; }
    
    // Validation properties
    /// <summary>
    /// True if the crash log contains enough information to be analyzed
    /// </summary>
    public bool IsComplete => Plugins.Count > 0;
    
    /// <summary>
    /// True if the crash log contains an error message
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(MainError);
    
    /// <summary>
    /// Game version extracted from the crash log
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// True if the crash log is incomplete or truncated
    /// </summary>
    public bool IsIncomplete { get; set; }
    
    /// <summary>
    /// Parse a crash log from file asynchronously
    /// </summary>
    public static async Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            
            // TODO: Implement actual parsing logic based on Python implementation
            // For now, return a basic crash log
            return new CrashLog
            {
                FilePath = filePath,
                OriginalLines = lines.ToList(),
                GameVersion = "Unknown",
                MainError = "",
                CallStack = new List<string>(),
                Plugins = new Dictionary<string, string>(),
                IsIncomplete = false
            };
        }
        catch
        {
            return null;
        }
    }
}