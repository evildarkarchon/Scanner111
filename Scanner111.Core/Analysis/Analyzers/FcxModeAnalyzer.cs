using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Caching;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Reporting.Fragments;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzer that performs FCX mode checks and generates reports.
///     Integrates FcxModeHandler with the analysis pipeline.
/// </summary>
public sealed class FcxModeAnalyzer : AnalyzerBase
{
    private readonly IFcxModeHandler _fcxModeHandler;
    private readonly ISettingsService _settingsService;

    public FcxModeAnalyzer(
        ILogger<FcxModeAnalyzer> logger,
        ISettingsService settingsService,
        IFcxModeHandler fcxModeHandler)
        : base(logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _fcxModeHandler = fcxModeHandler ?? throw new ArgumentNullException(nameof(fcxModeHandler));
    }

    /// <inheritdoc />
    public override string Name => "FcxModeAnalyzer";

    /// <inheritdoc />
    public override string DisplayName => "FCX Mode File Integrity Analyzer";

    /// <inheritdoc />
    public override int Priority => 20; // Run early to cache results for other analyzers

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2); // FCX checks may take longer

    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting FCX mode analysis for {Path}", context.InputPath);

        try
        {
            // Load mod detection settings to check FCX mode
            var modSettings = await _settingsService.LoadModDetectionSettingsAsync(context, cancellationToken)
                .ConfigureAwait(false);

            // Store FCX mode status in context for other analyzers
            if (modSettings.FcxMode != null) context.SetSharedData("FcxMode", modSettings.FcxMode.Value);

            // Check if FCX mode should run
            if (!ShouldRunFcxCheck(modSettings))
            {
                LogDebug("FCX mode is not enabled, skipping file checks");
                return CreateFcxDisabledResult();
            }

            // Perform FCX checks (may use cached results)
            var stopwatch = Stopwatch.StartNew();
            await _fcxModeHandler.CheckFcxModeAsync(cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            // Generate comprehensive report fragments
            var detailedFragments = _fcxModeHandler.GetDetailedFragments().ToList();
            var fragment = detailedFragments.Any() 
                ? FcxReportFragments.CombineFragments(
                    "FCX Mode Analysis Results",
                    detailedFragments,
                    10)
                : _fcxModeHandler.GetFcxMessages();

            // Store FCX results in context for other analyzers to use
            StoreFcxResultsInContext(context);

            // Determine severity based on findings
            var severity = DetermineSeverity();

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = fragment,
                Severity = severity
            };

            // Add enhanced metadata
            result.AddMetadata("FcxMode", modSettings.FcxMode?.ToString() ?? "Not Configured");
            result.AddMetadata("MainFilesChecked",
                (!string.IsNullOrWhiteSpace(_fcxModeHandler.MainFilesCheck)).ToString());
            result.AddMetadata("GameFilesChecked",
                (!string.IsNullOrWhiteSpace(_fcxModeHandler.GameFilesCheck)).ToString());
            result.AddMetadata("CheckDuration", stopwatch.ElapsedMilliseconds.ToString());
            result.AddMetadata("FragmentCount", detailedFragments.Count.ToString());

            LogInformation("FCX mode analysis completed with severity: {Severity}", severity);

            return result;
        }
        catch (OperationCanceledException)
        {
            LogDebug("FCX mode analysis was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to analyze FCX mode");
            return AnalysisResult.CreateFailure(Name, $"FCX mode analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        // FCX mode analyzer can run for any analysis type
        var canAnalyze = await base.CanAnalyzeAsync(context).ConfigureAwait(false);

        if (canAnalyze)
            // Additional check: only run if we have valid settings
            try
            {
                var modSettings = await _settingsService.LoadModDetectionSettingsAsync(context)
                    .ConfigureAwait(false);
                return modSettings != null;
            }
            catch
            {
                return false;
            }

        return canAnalyze;
    }

    private bool ShouldRunFcxCheck(ModDetectionSettings modSettings)
    {
        // FCX checks should run if explicitly enabled
        return modSettings.FcxMode == true;
    }

    private AnalysisResult CreateFcxDisabledResult()
    {
        var fragment = FcxReportFragments.CreateStatusFragment(
            fcxEnabled: false,
            mainFilesResult: null,
            modFilesResult: null,
            order: 10);

        var result = new AnalysisResult(Name)
        {
            Success = true,
            Fragment = fragment,
            Severity = AnalysisSeverity.Info
        };

        result.AddMetadata("FcxMode", "Disabled");
        result.AddMetadata("FragmentType", "StatusOnly");

        return result;
    }

    private void StoreFcxResultsInContext(AnalysisContext context)
    {
        // Store FCX check results for other analyzers to reference
        if (!string.IsNullOrWhiteSpace(_fcxModeHandler.MainFilesCheck))
            context.SetSharedData("FcxMainFilesResult", _fcxModeHandler.MainFilesCheck);

        if (!string.IsNullOrWhiteSpace(_fcxModeHandler.GameFilesCheck))
            context.SetSharedData("FcxGameFilesResult", _fcxModeHandler.GameFilesCheck);

        context.SetSharedData("FcxChecksCompleted", true);
        
        // Store detailed fragments for other analyzers
        var detailedFragments = _fcxModeHandler.GetDetailedFragments().ToList();
        if (detailedFragments.Any())
        {
            context.SetSharedData("FcxDetailedFragments", detailedFragments);
        }
        
        // Store error fragment if any
        var errorFragment = _fcxModeHandler.GetErrorOnlyFragment();
        if (errorFragment != null)
        {
            context.SetSharedData("FcxErrorFragment", errorFragment);
        }
    }

    private AnalysisSeverity DetermineSeverity()
    {
        // Check for errors or warnings in FCX results
        var mainFiles = _fcxModeHandler.MainFilesCheck ?? string.Empty;
        var gameFiles = _fcxModeHandler.GameFilesCheck ?? string.Empty;

        var hasErrors = mainFiles.Contains("❌", StringComparison.Ordinal) ||
                        gameFiles.Contains("❌", StringComparison.Ordinal);

        var hasWarnings = mainFiles.Contains("⚠️", StringComparison.Ordinal) ||
                          gameFiles.Contains("⚠️", StringComparison.Ordinal);

        if (hasErrors)
            return AnalysisSeverity.Error;
        if (hasWarnings)
            return AnalysisSeverity.Warning;

        return AnalysisSeverity.Info;
    }
}