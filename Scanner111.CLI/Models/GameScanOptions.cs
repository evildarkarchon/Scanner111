namespace Scanner111.CLI.Models;

/// <summary>
///     Command-line options for the game scan command.
/// </summary>
[Verb("gamescan", HelpText = "Perform comprehensive game configuration scans")]
public class GameScanOptions
{
    [Option('a', "all", Required = false, Default = true, HelpText = "Run all game scans (default)")]
    public bool All { get; set; } = true;

    [Option('c', "crashgen", Required = false, HelpText = "Check only Crash Generator (Buffout4) configuration")]
    public bool CrashGen { get; set; }

    [Option('x', "xse", Required = false, HelpText = "Validate only XSE plugins and Address Library")]
    public bool XsePlugins { get; set; }

    [Option('i', "ini", Required = false, HelpText = "Scan only mod INI files")]
    public bool ModInis { get; set; }

    [Option('w', "wrye", Required = false, HelpText = "Analyze only Wrye Bash plugin checker report")]
    public bool WryeBash { get; set; }

    [Option('e', "export", Required = false, HelpText = "Export scan results to specified file path")]
    public string? ExportPath { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }

    [Option("auto-fix", Required = false, Default = false, HelpText = "Automatically apply safe fixes where possible")]
    public bool AutoFix { get; set; }

    [Option("no-colors", Required = false, HelpText = "Disable colored output")]
    public bool DisableColors { get; set; }
}