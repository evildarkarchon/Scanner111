using CommandLine;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Commands;

[Verb("analyze", HelpText = "Analyze a crash log file")]
public class AnalyzeCommand
{
    [Option('f', "file", Required = true, HelpText = "Path to the crash log file")]
    public string LogFile { get; set; } = string.Empty;
    
    [Option('a', "analyzers", Separator = ',', HelpText = "Comma-separated list of analyzers to run")]
    public IEnumerable<string> Analyzers { get; set; } = Enumerable.Empty<string>();
    
    [Option('o', "output", HelpText = "Output file path for the report")]
    public string? OutputFile { get; set; }
    
    [Option('F', "format", Default = ReportFormat.Markdown, HelpText = "Output format (Markdown, Html, Json, Text)")]
    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
    
    [Option('v', "verbose", Default = false, HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
}