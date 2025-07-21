using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("scan", true, HelpText = "Scan crash log files for issues")]
public class ScanOptions
{
    [Option('l', "log", HelpText = "Path to specific crash log file")]
    public string? LogFile { get; set; }

    [Option('d', "scan-dir", HelpText = "Directory to scan for crash logs")]
    public string? ScanDir { get; set; }

    [Option('g', "game-path", HelpText = "Path to game installation directory")]
    public string? GamePath { get; set; }

    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }

    [Option("fcx-mode", HelpText = "Enable FCX mode for enhanced file checks")]
    public bool? FcxMode { get; set; }

    [Option("show-fid-values", HelpText = "Show FormID values (slower scans)")]
    public bool? ShowFidValues { get; set; }

    [Option("simplify-logs", HelpText = "Simplify logs (Warning: May remove important information)")]
    public bool? SimplifyLogs { get; set; }

    [Option("move-unsolved", HelpText = "Move unsolved logs to separate folder")]
    public bool? MoveUnsolved { get; set; }

    [Option("crash-logs-dir", HelpText = "Directory to store copied crash logs (with game subfolders)")]
    public string? CrashLogsDirectory { get; set; }

    [Option("skip-xse-copy", HelpText = "Skip automatic XSE (F4SE/SKSE) crash log copying")]
    public bool SkipXseCopy { get; set; }

    [Option("disable-progress", HelpText = "Disable progress bars in CLI mode")]
    public bool DisableProgress { get; set; }

    [Option("disable-colors", HelpText = "Disable colored output")]
    public bool DisableColors { get; set; }

    [Option('o', "output-format", Default = "detailed", HelpText = "Output format: detailed or summary")]
    public string OutputFormat { get; set; } = "detailed";
}