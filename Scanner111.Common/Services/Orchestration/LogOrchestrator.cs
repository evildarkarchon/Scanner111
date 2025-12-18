using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.Parsing;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Orchestrates the analysis pipeline for a single crash log.
/// </summary>
public class LogOrchestrator : ILogOrchestrator
{
    private readonly ILogger<LogOrchestrator> _logger;
    private readonly IFileIOService _fileIO;
    private readonly ILogParser _parser;
    private readonly IPluginAnalyzer _pluginAnalyzer;
    private readonly ISuspectScanner _suspectScanner;
    private readonly ISettingsScanner _settingsScanner;
    private readonly IReportWriter _reportWriter;
    private readonly IConfigurationCache _configCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogOrchestrator"/> class.
    /// </summary>
    public LogOrchestrator(
        ILogger<LogOrchestrator> logger,
        IFileIOService fileIO,
        ILogParser parser,
        IPluginAnalyzer pluginAnalyzer,
        ISuspectScanner suspectScanner,
        ISettingsScanner settingsScanner,
        IReportWriter reportWriter,
        IConfigurationCache configCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileIO = fileIO ?? throw new ArgumentNullException(nameof(fileIO));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _pluginAnalyzer = pluginAnalyzer ?? throw new ArgumentNullException(nameof(pluginAnalyzer));
        _suspectScanner = suspectScanner ?? throw new ArgumentNullException(nameof(suspectScanner));
        _settingsScanner = settingsScanner ?? throw new ArgumentNullException(nameof(settingsScanner));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
    }

    /// <inheritdoc/>
    public async Task<LogAnalysisResult> ProcessLogAsync(
        string logFilePath,
        ScanConfig config,
        CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(logFilePath);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Processing crash log: {FileName}", fileName);

        // 1. Read log file
        var content = await _fileIO.ReadFileAsync(logFilePath, ct).ConfigureAwait(false);
        _logger.LogDebug("Read {ByteCount} bytes from {FileName}", content.Length, fileName);

        // 2. Parse into segments
        var parseResult = await _parser.ParseAsync(content, ct).ConfigureAwait(false);
        if (!parseResult.IsValid)
        {
            _logger.LogWarning("Invalid crash log '{FileName}': {ErrorMessage}", fileName, parseResult.ErrorMessage ?? "Unknown parsing error");

            var failureReport = new ReportFragment { Lines = new[] { "# Analysis Failed", "", "Invalid or incomplete crash log.", "", $"**Reason**: {parseResult.ErrorMessage ?? "Unknown parsing error."}" } };
            await _reportWriter.WriteReportAsync(logFilePath, failureReport, ct).ConfigureAwait(false);

            return new LogAnalysisResult
            {
                LogFileName = fileName,
                Header = parseResult.Header,
                Segments = parseResult.Segments,
                Report = failureReport,
                IsComplete = false,
                Warnings = new[] { parseResult.ErrorMessage ?? "Unknown parsing error." }
            };
        }

        // Determine game name from header
        var gameName = DetectGameName(parseResult.Header);
        _logger.LogDebug("Detected game: {GameName}", gameName);

        // Fetch configuration data
        var suspectPatternsTask = _configCache.GetSuspectPatternsAsync(gameName, ct);
        var gameSettingsTask = _configCache.GetGameSettingsAsync(gameName, ct);

        await Task.WhenAll(suspectPatternsTask, gameSettingsTask).ConfigureAwait(false);
        var suspectPatterns = await suspectPatternsTask.ConfigureAwait(false);
        var gameSettings = await gameSettingsTask.ConfigureAwait(false);

        // 3. Run analysis components in parallel
        _logger.LogDebug("Running analysis components for {FileName}", fileName);
        var (pluginResult, suspectResult, settingsResult) =
            await RunAnalysisAsync(parseResult, suspectPatterns, gameSettings, ct).ConfigureAwait(false);

        // 4. Build report
        var report = BuildReport(parseResult, pluginResult, suspectResult, settingsResult, gameName);

        // 5. Write report file
        await _reportWriter.WriteReportAsync(logFilePath, report, ct).ConfigureAwait(false);

        stopwatch.Stop();
        _logger.LogInformation("Processed crash log '{FileName}' in {Duration:F2}s", fileName, stopwatch.Elapsed.TotalSeconds);

        return new LogAnalysisResult
        {
            LogFileName = fileName,
            Header = parseResult.Header,
            Segments = parseResult.Segments,
            Report = report,
            IsComplete = parseResult.Segments.Any(s => s.Name.Contains("PLUGINS", StringComparison.OrdinalIgnoreCase)),
            Warnings = pluginResult.Warnings.Concat(suspectResult.Recommendations).ToList()
        };
    }

    private async Task<(PluginAnalysisResult, SuspectScanResult, SettingsScanResult?)>
        RunAnalysisAsync(
            LogParseResult parseResult, 
            SuspectPatterns patterns, 
            GameSettings gameSettings,
            CancellationToken ct)
    {
        // Run analyzers in parallel
        var pluginTask = _pluginAnalyzer.AnalyzeAsync(parseResult.Segments, ct);
        var suspectTask = _suspectScanner.ScanAsync(
            parseResult.Header, parseResult.Segments, patterns, ct);

        Task<SettingsScanResult>? settingsTask = null;
        var compatibilitySegment = parseResult.Segments.FirstOrDefault(s => s.Name.Contains("Compatibility", StringComparison.OrdinalIgnoreCase));
        if (compatibilitySegment != null)
        {
            settingsTask = _settingsScanner.ScanAsync(compatibilitySegment, gameSettings, ct);
        }

        await Task.WhenAll(pluginTask, suspectTask, settingsTask ?? Task.CompletedTask).ConfigureAwait(false);

        return (await pluginTask.ConfigureAwait(false), await suspectTask.ConfigureAwait(false), settingsTask != null ? await settingsTask.ConfigureAwait(false) : null);
    }

    private ReportFragment BuildReport(
        LogParseResult parseResult, 
        PluginAnalysisResult pluginResult, 
        SuspectScanResult suspectResult, 
        SettingsScanResult? settingsResult,
        string gameName)
    {
        var builder = new ReportBuilder();

        // Header
        builder.Add(ReportSections.CreateHeader(parseResult.Header, gameName));

        // Main Error
        if (!string.IsNullOrWhiteSpace(parseResult.Header.MainError))
        {
             // Often already in header, but maybe emphasized
        }

        // Suspects (Error Matches)
        if (suspectResult.ErrorMatches.Any())
        {
            builder.AddSection("Main Error Suspects", suspectResult.ErrorMatches);
        }
        
        // Suspects (Stack Matches)
        if (suspectResult.StackMatches.Any())
        {
            builder.AddSection("Stack Trace Suspects", suspectResult.StackMatches);
        }

        // Recommendations
        builder.Add(ReportSections.CreateRecommendationsSection(suspectResult.Recommendations));

        // Plugins
        builder.Add(ReportSections.CreatePluginSummary(pluginResult.Plugins));
        
        // Warnings
        builder.Add(ReportSections.CreateWarningsSection(pluginResult.Warnings));

        return builder.Build();
    }

    private string DetectGameName(CrashHeader header)
    {
        if (header == null || string.IsNullOrWhiteSpace(header.GameVersion))
        {
            return "Fallout 4"; // Default
        }

        if (header.GameVersion.Contains("Skyrim", StringComparison.OrdinalIgnoreCase))
        {
            return "Skyrim Special Edition";
        }

        return "Fallout 4";
    }
}
