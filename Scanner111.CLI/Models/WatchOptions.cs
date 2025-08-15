namespace Scanner111.CLI.Models;

[Verb("watch", HelpText = "Monitor directory for new crash logs and auto-analyze them")]
public class WatchOptions
{
    [Option('p', "path", Required = false, HelpText = "Path to monitor for crash logs")]
    public string? Path { get; set; }

    [Option('g', "game", Required = false, HelpText = "Game to monitor (Fallout4, Skyrim)")]
    public string? Game { get; set; }

    [Option("scan-existing", Required = false, Default = false, HelpText = "Scan existing logs on startup")]
    public bool ScanExisting { get; set; }

    [Option("auto-move", Required = false, Default = false, HelpText = "Automatically move analyzed logs")]
    public bool AutoMove { get; set; }

    [Option("notification", Required = false, Default = true, HelpText = "Show notifications for new logs")]
    public bool ShowNotifications { get; set; }

    [Option("dashboard", Required = false, Default = true, HelpText = "Show dashboard view")]
    public bool ShowDashboard { get; set; }

    [Option("pattern", Required = false, Default = "*.log", HelpText = "File pattern to watch")]
    public string Pattern { get; set; } = "*.log";

    [Option("recursive", Required = false, Default = false, HelpText = "Monitor subdirectories")]
    public bool Recursive { get; set; }

    [Option("fcx-mode", Required = false, Default = false, HelpText = "Enable FCX mode for enhanced file checks")]
    public bool FcxMode { get; set; }

    [Option("verbose", Required = false, Default = false, HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
}