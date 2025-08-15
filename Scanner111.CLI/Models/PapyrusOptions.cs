using Scanner111.Core.Models;

namespace Scanner111.CLI.Models;

[Verb("papyrus", HelpText = "Monitor and analyze Papyrus log files")]
public class PapyrusOptions
{
    [Option('p', "path", HelpText = "Path to the Papyrus.0.log file")]
    public string? LogPath { get; set; }

    [Option('g', "game", HelpText = "Game type for auto-detection (Fallout4 or Skyrim)")]
    public GameType? GameType { get; set; }

    [Option('i', "interval", Default = 1000, HelpText = "Monitoring interval in milliseconds")]
    public int Interval { get; set; }

    [Option('d', "dashboard", HelpText = "Show live dashboard with statistics")]
    public bool ShowDashboard { get; set; }

    [Option('e', "export", HelpText = "Export path for statistics")]
    public string? ExportPath { get; set; }

    [Option('f', "format", Default = "csv", HelpText = "Export format (csv or json)")]
    public string ExportFormat { get; set; } = "csv";

    [Option('h', "history", Default = 1000, HelpText = "Maximum history entries to keep")]
    public int HistoryLimit { get; set; }

    [Option('o', "once", HelpText = "Analyze once without continuous monitoring")]
    public bool AnalyzeOnce { get; set; }

    [Option("error-threshold", Default = 100, HelpText = "Error count threshold for alerts")]
    public int ErrorThreshold { get; set; }

    [Option("warning-threshold", Default = 500, HelpText = "Warning count threshold for alerts")]
    public int WarningThreshold { get; set; }

    [Option("auto-export", HelpText = "Automatically export statistics periodically")]
    public bool AutoExport { get; set; }

    [Option("export-interval", Default = 300000,
        HelpText = "Auto-export interval in milliseconds (default: 5 minutes)")]
    public int ExportInterval { get; set; }

    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
}