using Scanner111.Services; // For SeverityLevel

namespace Scanner111.Models
{
    public class WarningDetails
    {
        public string? Title { get; set; }
        public required string Message { get; set; }
        public string? Recommendation { get; set; }
        public SeverityLevel Severity { get; set; } = SeverityLevel.Warning;
    }

    public class ConflictRule
    {
        public required string PluginA { get; set; }
        public required string PluginB { get; set; }
        public string? Title { get; set; }
        public required string Message { get; set; }
        public string? Recommendation { get; set; }
        public SeverityLevel Severity { get; set; } = SeverityLevel.Warning;
    }
}
