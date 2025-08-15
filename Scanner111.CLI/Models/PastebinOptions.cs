namespace Scanner111.CLI.Models;

[Verb("pastebin", HelpText = "Fetch and analyze crash logs from Pastebin")]
public class PastebinOptions
{
    [Option('u', "url", HelpText = "Pastebin URL or ID to fetch")]
    public string? UrlOrId { get; set; }

    [Option('m', "multiple", Separator = ',', HelpText = "Multiple Pastebin URLs or IDs to fetch (comma-separated)")]
    public IEnumerable<string>? Multiple { get; set; }

    [Option('f', "file", HelpText = "File containing list of Pastebin URLs or IDs (one per line)")]
    public string? InputFile { get; set; }

    [Option('s', "scan", Default = false, HelpText = "Scan fetched logs after download")]
    public bool ScanAfterDownload { get; set; }

    [Option('o', "output-dir", HelpText = "Custom output directory for fetched logs")]
    public string? OutputDirectory { get; set; }

    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }

    [Option("no-colors", HelpText = "Disable colored output")]
    public bool NoColors { get; set; }

    [Option("fcx-mode", HelpText = "Enable FCX mode for enhanced file checks on fetched logs")]
    public bool FcxMode { get; set; }

    [Option("show-fid-values", HelpText = "Show FormID values in scan results")]
    public bool ShowFidValues { get; set; }

    [Option("simplify-logs", HelpText = "Simplify logs (Warning: May remove important information)")]
    public bool SimplifyLogs { get; set; }

    [Option("parallel", Default = 3, HelpText = "Number of parallel downloads (1-10)")]
    public int ParallelDownloads { get; set; }

    [Option("timeout", Default = 30, HelpText = "Download timeout in seconds")]
    public int TimeoutSeconds { get; set; }
}