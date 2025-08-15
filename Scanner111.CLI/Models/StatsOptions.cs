using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("stats", HelpText = "View scan statistics and history")]
public class StatsOptions
{
    [Option('p', "period", Required = false, HelpText = "Time period for statistics (today, week, month, all)",
        Default = "week")]
    public string Period { get; set; } = "week";

    [Option('d', "detailed", Required = false, HelpText = "Show detailed statistics")]
    public bool Detailed { get; set; }

    [Option('g', "game", Required = false, HelpText = "Filter by game type (Fallout4, Skyrim)")]
    public string? GameType { get; set; }

    [Option('c', "clear", Required = false, HelpText = "Clear all statistics")]
    public bool Clear { get; set; }

    [Option('e', "export", Required = false, HelpText = "Export statistics to CSV file")]
    public string? ExportPath { get; set; }

    [Option('t', "top", Required = false, HelpText = "Show top N most common issues", Default = 10)]
    public int TopIssues { get; set; } = 10;

    [Option('r', "recent", Required = false, HelpText = "Show N most recent scans", Default = 10)]
    public int RecentScans { get; set; } = 10;
}