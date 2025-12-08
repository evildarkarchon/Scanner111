namespace Scanner111.Models;

public record LogAnalysisResultDisplay
{
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty; // For error messages or brief summaries
    public string Content { get; init; } = string.Empty; // Full Markdown content
}
