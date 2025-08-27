using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzes crash generator and mod settings for potential issues and conflicts.
///     Thread-safe for concurrent analysis operations.
/// </summary>
public sealed class SettingsAnalyzer : AnalyzerBase, ISettingsAnalyzer
{
    private readonly MemoryManagementValidator _memoryValidator;
    private readonly ISettingsService _settingsService;

    public SettingsAnalyzer(
        ILogger<SettingsAnalyzer> logger,
        ISettingsService settingsService,
        MemoryManagementValidator memoryValidator)
        : base(logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _memoryValidator = memoryValidator ?? throw new ArgumentNullException(nameof(memoryValidator));
    }

    /// <inheritdoc />
    public override string Name => "SettingsAnalyzer";

    /// <inheritdoc />
    public override string DisplayName => "Settings Configuration Analyzer";

    /// <inheritdoc />
    public override int Priority => 50; // Higher priority to run early

    /// <inheritdoc />
    public Task<ReportFragment> ScanAchievementsSettingAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default)
    {
        var crashGenAchievements = settings.Achievements ?? false;
        var hasConflict = crashGenAchievements &&
                          (modSettings.HasXseModule("achievements.dll") ||
                           modSettings.HasXseModule("unlimitedsurvivalmode.dll"));

        ReportFragment fragment;
        if (hasConflict)
        {
            LogWarning("Achievements conflict detected");
            fragment = ReportFragmentBuilder
                .CreateWarning(
                    "The Achievements Mod and/or Unlimited Survival Mode is installed, but Achievements is set to TRUE",
                    $"Open {settings.CrashGenName}'s TOML file and change Achievements to FALSE, this prevents conflicts with {settings.CrashGenName}.")
                .WithTitle("Achievements Settings")
                .AppendSeparator()
                .Build();
        }
        else
        {
            LogDebug("Achievements setting is correct");
            fragment = ReportFragmentBuilder
                .CreateSuccess(
                    $"Achievements parameter is correctly configured in your {settings.CrashGenName} settings!")
                .WithTitle("Achievements Settings")
                .AppendSeparator()
                .Build();
        }

        return Task.FromResult(fragment);
    }

    /// <inheritdoc />
    public Task<ReportFragment> ScanMemoryManagementSettingsAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the specialized validator
        var fragment = _memoryValidator.Validate(settings, modSettings);
        return Task.FromResult(fragment);
    }

    /// <inheritdoc />
    public Task<ReportFragment> ScanArchiveLimitSettingAsync(
        CrashGenSettings settings,
        CancellationToken cancellationToken = default)
    {
        // Skip check for versions >= 1.29.0
        if (settings.Version != null && settings.Version >= new Version(1, 29, 0))
        {
            LogDebug("Skipping ArchiveLimit check for version {Version}", settings.Version);
            return Task.FromResult(ReportFragment.CreateInfo(
                "Archive Limit Settings",
                "Archive limit check skipped for version 1.29.0 and above.",
                300));
        }

        var archiveLimit = settings.ArchiveLimit ?? false;

        ReportFragment fragment;
        if (archiveLimit)
        {
            LogWarning("ArchiveLimit is enabled (known to cause instability)");
            fragment = ReportFragmentBuilder
                .CreateWarning(
                    "ArchiveLimit is set to TRUE, this setting is known to cause instability.",
                    $"Open {settings.CrashGenName}'s TOML file and change ArchiveLimit to FALSE.")
                .WithTitle("Archive Limit Settings")
                .AppendSeparator()
                .Build();
        }
        else
        {
            LogDebug("ArchiveLimit setting is correct");
            fragment = ReportFragmentBuilder
                .CreateSuccess(
                    $"ArchiveLimit parameter is correctly configured in your {settings.CrashGenName} settings!")
                .WithTitle("Archive Limit Settings")
                .AppendSeparator()
                .Build();
        }

        return Task.FromResult(fragment);
    }

    /// <inheritdoc />
    public Task<ReportFragment> ScanLooksMenuSettingAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default)
    {
        var f4ee = settings.F4EE;

        ReportFragment fragment;
        if (f4ee.HasValue)
        {
            var hasConflict = !f4ee.Value && modSettings.HasXseModule("f4ee.dll");

            if (hasConflict)
            {
                LogWarning("Looks Menu installed but F4EE is disabled");
                fragment = ReportFragmentBuilder
                    .CreateWarning(
                        "Looks Menu is installed, but F4EE parameter under [Compatibility] is set to FALSE",
                        $"Open {settings.CrashGenName}'s TOML file and change F4EE to TRUE, this prevents bugs and crashes from Looks Menu.")
                    .WithTitle("Looks Menu (F4EE) Settings")
                    .AppendSeparator()
                    .Build();
            }
            else
            {
                LogDebug("F4EE setting is correct");
                fragment = ReportFragmentBuilder
                    .CreateSuccess(
                        $"F4EE (Looks Menu) parameter is correctly configured in your {settings.CrashGenName} settings!")
                    .WithTitle("Looks Menu (F4EE) Settings")
                    .AppendSeparator()
                    .Build();
            }
        }
        else
        {
            LogDebug("F4EE setting not found in configuration");
            fragment = ReportFragment.CreateInfo("Looks Menu (F4EE) Settings", "F4EE setting not configured.", 250);
        }

        return Task.FromResult(fragment);
    }

    /// <inheritdoc />
    public Task<ReportFragment> CheckDisabledSettingsAsync(
        CrashGenSettings settings,
        CancellationToken cancellationToken = default)
    {
        var builder = ReportFragmentBuilder
            .Create()
            .WithTitle("Disabled Settings Check")
            .WithType(FragmentType.Info)
            .WithOrder(300);

        var foundDisabled = 0;

        foreach (var kvp in settings.RawSettings)
        {
            // Skip if in ignored list
            if (settings.IgnoredSettings.Contains(kvp.Key))
                continue;

            // Check if setting is explicitly false
            var isFalse = kvp.Value switch
            {
                bool b => !b,
                string s when bool.TryParse(s, out var parsed) => !parsed,
                int i => i == 0,
                _ => false
            };

            if (isFalse)
            {
                builder.AppendNotice(
                    $"{kvp.Key} is disabled in your {settings.CrashGenName} settings, is this intentional?");
                builder.AppendSeparator();
                foundDisabled++;
                LogDebug("Disabled setting found: {Setting}", kvp.Key);
            }
        }

        if (foundDisabled > 0) LogInformation("Found {Count} disabled settings", foundDisabled);

        var fragment = foundDisabled > 0
            ? builder.Build()
            : ReportFragment.CreateInfo("Disabled Settings Check", "No unexpected disabled settings found.", 300);

        return Task.FromResult(fragment);
    }

    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting settings analysis for {Path}", context.InputPath);

        try
        {
            // Load settings
            var crashGenSettings = await _settingsService.LoadCrashGenSettingsAsync(context, cancellationToken)
                .ConfigureAwait(false);
            var modSettings = await _settingsService.LoadModDetectionSettingsAsync(context, cancellationToken)
                .ConfigureAwait(false);

            // Run all scans in parallel for efficiency
            var scanTasks = new List<Task<ReportFragment>>
            {
                ScanAchievementsSettingAsync(crashGenSettings, modSettings, cancellationToken),
                ScanMemoryManagementSettingsAsync(crashGenSettings, modSettings, cancellationToken),
                ScanArchiveLimitSettingAsync(crashGenSettings, cancellationToken),
                ScanLooksMenuSettingAsync(crashGenSettings, modSettings, cancellationToken),
                CheckDisabledSettingsAsync(crashGenSettings, cancellationToken)
            };

            var fragments = await Task.WhenAll(scanTasks).ConfigureAwait(false);

            // Combine all fragments
            var combinedFragment = CombineFragments(fragments);

            // Determine severity based on findings
            var severity = DetermineSeverity(fragments);

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = combinedFragment,
                Severity = severity
            };

            // Add metadata
            result.AddMetadata("CrashGenName", crashGenSettings.CrashGenName);
            result.AddMetadata("CrashGenVersion", crashGenSettings.Version?.ToString() ?? "Unknown");
            result.AddMetadata("XseModuleCount", modSettings.XseModules.Count.ToString());

            LogInformation("Settings analysis completed with severity: {Severity}", severity);

            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to analyze settings");
            return AnalysisResult.CreateFailure(Name, $"Settings analysis failed: {ex.Message}");
        }
    }


    private static ReportFragment CombineFragments(ReportFragment[] fragments)
    {
        var combined = ReportFragmentExtensions.CombineFragments(
            "Settings Analysis",
            fragments,
            50);

        return combined ?? ReportFragment.CreateSection(
            "Settings Analysis",
            "No settings issues detected.",
            50);
    }

    private static AnalysisSeverity DetermineSeverity(ReportFragment[] fragments)
    {
        var hasErrors = fragments.Any(f => f?.Type == FragmentType.Error);
        var hasWarnings = fragments.Any(f => f?.Type == FragmentType.Warning);

        if (hasErrors)
            return AnalysisSeverity.Error;
        if (hasWarnings)
            return AnalysisSeverity.Warning;

        return AnalysisSeverity.Info;
    }
}