using System.Collections.Generic;

namespace Scanner111.Models
{
    /// <summary>
    /// Structure to hold information about issues detected in logs
    /// </summary>
    public class LogIssue
    {
        public string? FileName { get; set; }
        public int LineNumber { get; set; } // Optional, if applicable
        public string IssueId { get; set; } = "GenericIssue"; // A unique ID for the type of issue
        public string Title { get; set; } = string.Empty; // A short title for the issue
        public string Message { get; set; } = string.Empty; // Detailed description
        public string Details { get; set; } = string.Empty; // Additional detailed information
        public string Recommendation { get; set; } = string.Empty; // How to fix it
        public SeverityLevel Severity { get; set; } = SeverityLevel.Information;
        public string? Source { get; set; } // e.g., "BuffoutCheck", "ModConflict"
    }

    /// <summary>
    /// Severity levels for log issues
    /// </summary>
    public enum SeverityLevel
    {
        Critical,
        Error,
        Warning,
        Information,
        Debug
    }

    /// <summary>
    /// Class to hold structured information parsed from the crash log
    /// </summary>
    public class ParsedCrashLog
    {
        public string FilePath { get; }
        public List<string> Lines { get; }
        public string? CrashGeneratorName { get; set; } // e.g., Buffout 4, CLAS
        public string? GameVersion { get; set; }
        public Dictionary<string, string> LoadedPlugins { get; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public List<string> CallStack { get; } = new List<string>();
        public List<string> MainErrorSegment { get; } = new List<string>();
        public Dictionary<string, List<string>> OtherSegments { get; } = new Dictionary<string, List<string>>(); // For other named segments

        public ParsedCrashLog(string filePath, List<string> lines)
        {
            FilePath = filePath;
            Lines = lines;
        }
    }
}
