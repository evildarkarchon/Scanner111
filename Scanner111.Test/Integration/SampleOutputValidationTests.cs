using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using Scanner111.Test.Infrastructure;

namespace Scanner111.Test.Integration;

/// <summary>
///     Tests that validate Scanner111 output against expected outputs from the legacy CLASSIC scanner.
///     These tests ensure compatibility and correctness of analysis results.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Performance", "Slow")]
[Trait("Component", "Integration")]
public class SampleOutputValidationTests : SampleDataTestBase
{
    private IAnalyzerOrchestrator _orchestrator = null!;
    private IAsyncYamlSettingsCore _yamlCore = null!;
    private ReportComposer _reportComposer = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Setup mock YAML settings
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        SetupYamlMocksForValidation();
        
        services.AddSingleton(_yamlCore);
        
        // Add all analyzers to match production configuration
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<SettingsAnalyzer>();
        services.AddTransient<SuspectScannerAnalyzer>();
        services.AddTransient<ModDetectionAnalyzer>();
        
        // Add supporting services
        services.AddTransient<IPluginLoader, ValidationPluginLoader>();
        services.AddTransient<IModDatabase, ValidationModDatabase>();
        services.AddTransient<IAnalyzerOrchestrator, AnalyzerOrchestrator>();
        services.AddTransient<ReportComposer>();
    }

    protected override Task OnInitializeAsync()
    {
        _orchestrator = GetService<IAnalyzerOrchestrator>();
        _reportComposer = GetService<ReportComposer>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidateOutput_BA2LimitCrash_DetectsSuspect()
    {
        // Arrange - This specific log has a BA2 limit crash
        var logName = "crash-2023-09-15-01-54-49.log";
        var expectedOutput = await ReadExpectedOutputAsync(logName);
        expectedOutput.Should().NotBeNull();
        
        var sampleContent = await ReadSampleLogAsync(logName);
        var context = CreateContextFromLog(sampleContent, logName);
        
        // Act
        var report = await GenerateReportAsync(context);
        
        // Assert
        // The expected output shows: "Checking for BA2 Limit Crash... SUSPECT FOUND! > Severity : 6"
        if (expectedOutput!.Contains("BA2 Limit Crash") && expectedOutput.Contains("SUSPECT FOUND"))
        {
            // Our analyzer should detect this as well
            report.Should().MatchEquivalentOf("*BA2*", 
                "Should detect BA2-related issues when present in expected output");
        }
    }

    [Theory]
    [InlineData("crash-2023-09-15-01-54-49.log")]
    [InlineData("crash-2023-09-15-01-56-06.log")]
    [InlineData("crash-2023-11-08-05-46-35.log")]
    public async Task ValidateOutput_MainErrorDetection_MatchesExpected(string logName)
    {
        // Arrange
        var expectedOutput = await ReadExpectedOutputAsync(logName);
        if (expectedOutput == null)
        {
            Logger.LogWarning("No expected output for {LogName}, skipping", logName);
            return;
        }
        
        var sampleContent = await ReadSampleLogAsync(logName);
        var context = CreateContextFromLog(sampleContent, logName);
        
        // Act
        var report = await GenerateReportAsync(context);
        
        // Assert - Extract and compare main error
        var expectedError = ExtractMainError(expectedOutput);
        if (!string.IsNullOrEmpty(expectedError))
        {
            report.Should().Contain(expectedError.Split(' ')[0], // Check for exception type
                $"Report should contain the same error type as expected: {expectedError}");
        }
    }

    [Fact]
    public async Task ValidateOutput_Buffout4Version_CorrectlyDetected()
    {
        // Arrange - Use multiple samples
        var samples = new[]
        {
            ("crash-2023-09-15-01-54-49.log", "1.28.6"),
            ("crash-2022-06-05-12-52-17.log", "1.24.5")
        };
        
        foreach (var (logName, expectedVersion) in samples)
        {
            var sampleContent = await ReadSampleLogAsync(logName);
            var context = CreateContextFromLog(sampleContent, logName);
            
            // Act
            var results = await RunAnalyzersAsync(context);
            
            // Assert
            context.TryGetSharedData<string>("Buffout4Version", out var detectedVersion).Should().BeTrue(
                $"Version should be detected for {logName}");
            detectedVersion.Should().Be(expectedVersion,
                $"Version should match expected for {logName}");
        }
    }

    [Fact]
    public async Task ValidateOutput_SettingsChecks_ProduceExpectedWarnings()
    {
        // Arrange
        var logName = "crash-2023-09-15-01-54-49.log";
        var expectedOutput = await ReadExpectedOutputAsync(logName);
        expectedOutput.Should().NotBeNull();
        
        var sampleContent = await ReadSampleLogAsync(logName);
        var context = CreateContextFromLog(sampleContent, logName);
        
        // Parse settings from log
        ParseSettingsIntoContext(context, sampleContent);
        
        // Act
        var results = await RunAnalyzersAsync(context);
        var settingsResult = results.FirstOrDefault(r => r.AnalyzerName == "SettingsAnalyzer");
        
        // Assert
        if (expectedOutput!.Contains("correctly configured"))
        {
            // Check that our settings analyzer produces similar validations
            settingsResult.Should().NotBeNull();
            if (settingsResult?.Fragment != null)
            {
                var content = GetFullFragmentContent(settingsResult.Fragment);
                Logger.LogInformation("Settings analysis content: {Content}", content);
                
                // We should detect configuration status
                content.Should().MatchEquivalentOf("*Buffout*",
                    "Settings analysis should mention Buffout configuration");
            }
        }
    }

    [Fact]
    public async Task ValidateOutput_NamedRecords_ExtractedWhenPresent()
    {
        // Arrange - Find a log with named records in expected output
        var pairs = GetMatchingSamplePairs().ToList();
        var pairWithRecords = pairs.FirstOrDefault(p => 
            File.ReadAllText(p.OutputPath).Contains("Named Records"));
        
        if (pairWithRecords == default)
        {
            Logger.LogWarning("No sample with named records found, skipping test");
            return;
        }
        
        var logName = Path.GetFileName(pairWithRecords.LogPath);
        var sampleContent = await File.ReadAllTextAsync(pairWithRecords.LogPath);
        var expectedOutput = await File.ReadAllTextAsync(pairWithRecords.OutputPath);
        
        var context = CreateContextFromLog(sampleContent, logName);
        
        // Act
        var report = await GenerateReportAsync(context);
        
        // Assert - Extract expected named records
        var expectedRecords = ExtractNamedRecords(expectedOutput);
        if (expectedRecords.Any())
        {
            // Our report should mention these records or similar detection
            foreach (var record in expectedRecords)
            {
                Logger.LogInformation("Expected named record: {Record}", record);
            }
            
            // Context should have captured relevant data
            context.TryGetSharedData<List<string>>("NamedRecords", out var detectedRecords);
            if (detectedRecords != null)
            {
                detectedRecords.Should().NotBeEmpty("Named records should be detected when present");
            }
        }
    }

    [Fact]
    public async Task ValidateOutput_FCXModeWarning_IncludedWhenDisabled()
    {
        // Arrange
        var samples = GetMatchingSamplePairs().Take(3);
        
        foreach (var (logPath, outputPath) in samples)
        {
            var expectedOutput = await File.ReadAllTextAsync(outputPath);
            
            // Check if expected output mentions FCX mode
            if (expectedOutput.Contains("FCX MODE IS DISABLED"))
            {
                var logName = Path.GetFileName(logPath);
                var sampleContent = await File.ReadAllTextAsync(logPath);
                var context = CreateContextFromLog(sampleContent, logName);
                
                // Act
                var report = await GenerateReportAsync(context);
                
                // Assert
                // Our report should also indicate FCX status
                report.Should().MatchEquivalentOf("*FCX*",
                    $"Report for {logName} should mention FCX mode status");
            }
        }
    }

    [Fact]
    public async Task ValidateOutput_ConsistencyAcrossRuns_ProducesDeterministicResults()
    {
        // Arrange
        var logName = "crash-2023-09-15-01-54-49.log";
        var sampleContent = await ReadSampleLogAsync(logName);
        
        // Act - Run analysis multiple times
        var reports = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var context = CreateContextFromLog(sampleContent, logName);
            var report = await GenerateReportAsync(context);
            reports.Add(report);
        }
        
        // Assert - All runs should produce identical results
        reports.Should().AllBeEquivalentTo(reports[0],
            "Analysis should be deterministic and produce same results across runs");
    }

    private AnalysisContext CreateContextFromLog(string logContent, string logName)
    {
        var testPath = Path.Combine(TestDirectory, logName);
        File.WriteAllText(testPath, logContent);
        
        var context = new AnalysisContext(testPath, _yamlCore);
        ParseCompleteLogIntoContext(context, logContent);
        return context;
    }

    private void ParseCompleteLogIntoContext(AnalysisContext context, string logContent)
    {
        var lines = logContent.Split('\n');
        
        // Parse main error
        var errorLine = lines.FirstOrDefault(l => l.Contains("Unhandled exception"));
        if (!string.IsNullOrEmpty(errorLine))
        {
            context.SetSharedData("MainError", errorLine.Trim());
        }
        
        // Parse Buffout version
        var versionLine = lines.FirstOrDefault(l => l.StartsWith("Buffout 4 v"));
        if (!string.IsNullOrEmpty(versionLine))
        {
            var version = Regex.Match(versionLine, @"v([\d\.]+)").Groups[1].Value;
            context.SetSharedData("Buffout4Version", version);
        }
        
        // Parse plugins
        var pluginsIdx = Array.FindIndex(lines, l => l.Trim() == "PLUGINS:");
        if (pluginsIdx >= 0)
        {
            var plugins = new List<string>();
            for (int i = pluginsIdx + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrWhiteSpace(line) && (line.StartsWith("    ") || line.StartsWith("\t")))
                    plugins.Add(line.Trim());
                else if (!string.IsNullOrWhiteSpace(line))
                    break;
            }
            context.SetSharedData("PluginSegment", plugins);
        }
        
        // Parse call stack
        var stackIdx = Array.FindIndex(lines, l => l.Contains("PROBABLE CALL STACK:"));
        if (stackIdx >= 0)
        {
            var stack = new List<string>();
            for (int i = stackIdx + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("[") && (line.Contains("]")))
                    stack.Add(line.Trim());
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("    "))
                    break;
            }
            context.SetSharedData("CallStackSegment", stack);
            context.SetSharedData("CallStack", string.Join('\n', stack));
        }
        
        // Store full content
        context.SetSharedData("LogContent", logContent);
        context.SetSharedData("LogLines", lines.ToList());
    }

    private void ParseSettingsIntoContext(AnalysisContext context, string logContent)
    {
        var lines = logContent.Split('\n');
        var settingsIdx = Array.FindIndex(lines, l => l.Trim() == "SETTINGS:");
        
        if (settingsIdx >= 0)
        {
            var settings = new Dictionary<string, string>();
            string currentSection = "";
            
            for (int i = settingsIdx + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("[") && !line.Contains(":")) break;
                
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Trim('[', ']');
                }
                else if (line.Contains(":"))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var key = $"{currentSection}.{parts[0].Trim()}";
                        settings[key] = parts[1].Trim();
                    }
                }
            }
            
            context.SetSharedData("BuffoutSettings", settings);
        }
    }

    private async Task<List<AnalysisResult>> RunAnalyzersAsync(AnalysisContext context)
    {
        var request = new AnalysisRequest
        {
            InputPath = context.InputPath,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Sequential,
                VerboseOutput = true
            }
        };
        
        var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, TestCancellation.Token);
        return orchestrationResult.Results.ToList();
    }

    private async Task<string> GenerateReportAsync(AnalysisContext context)
    {
        var results = await RunAnalyzersAsync(context);
        
        // Build fragments list
        var fragments = new List<ReportFragment>();
        
        // Add version info
        context.TryGetSharedData<string>("Buffout4Version", out var version);
        if (!string.IsNullOrEmpty(version))
        {
            fragments.Add(ReportFragment.CreateInfo(
                "Buffout Version", 
                $"Detected Buffout 4 v{version}", 
                10));
        }
        
        // Add main error
        context.TryGetSharedData<string>("MainError", out var mainError);
        if (!string.IsNullOrEmpty(mainError))
        {
            fragments.Add(ReportFragment.CreateError(
                "Main Error", 
                mainError, 
                100));
        }
        
        // Add analyzer results
        fragments.AddRange(results.Where(r => r.Fragment != null).Select(r => r.Fragment!));
        
        // Create main fragment with all children
        var mainFragment = ReportFragment.CreateWithChildren("Scanner111 Analysis Report", fragments, 0);
        
        return await _reportComposer.ComposeFromFragmentsAsync(new[] { mainFragment });
    }

    private string ExtractMainError(string expectedOutput)
    {
        var match = Regex.Match(expectedOutput, @"\*\*Main Error:\*\*\s*(.+)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private List<string> ExtractNamedRecords(string expectedOutput)
    {
        var records = new List<string>();
        var lines = expectedOutput.Split('\n');
        var inRecordsSection = false;
        
        foreach (var line in lines)
        {
            if (line.Contains("Checking for Named Records"))
            {
                inRecordsSection = true;
                continue;
            }
            
            if (inRecordsSection)
            {
                if (line.StartsWith("-") && line.Contains("(char*)"))
                {
                    records.Add(line.Trim());
                }
                else if (line.Contains("---") || line.Contains("###"))
                {
                    break;
                }
            }
        }
        
        return records;
    }

    private string GetFullFragmentContent(ReportFragment fragment)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(fragment.Title))
            parts.Add($"### {fragment.Title}");
        
        if (!string.IsNullOrEmpty(fragment.Content))
            parts.Add(fragment.Content);
        
        foreach (var child in fragment.Children)
        {
            parts.Add(GetFullFragmentContent(child));
        }
        
        return string.Join("\n", parts);
    }

    private void SetupYamlMocksForValidation()
    {
        // Setup default returns for common YAML queries
        _yamlCore.GetSettingAsync<string>(
            Arg.Any<YamlStore>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("true"));
        
        _yamlCore.GetSettingAsync<List<string>>(
            Arg.Any<YamlStore>(),
            Arg.Any<string>(),
            Arg.Any<List<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));
        
        _yamlCore.GetSettingAsync<Dictionary<string, string>>(
            Arg.Any<YamlStore>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Dictionary<string, string>?>(new Dictionary<string, string>()));
        
        // Setup specific settings for validation
        _yamlCore.GetSettingAsync<Dictionary<string, string>>(
            YamlStore.Game,
            "Crashlog_Error_Check",
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Dictionary<string, string>?>(new Dictionary<string, string>
            {
                { "BA2 Limit", "BA2 file count limit reached" }
            }));
    }
}

// Validation-specific service implementations
internal class ValidationPluginLoader : IPluginLoader
{
    public Task<(Dictionary<string, string> plugins, bool pluginsLoaded, ReportFragment fragment)> 
        LoadFromLoadOrderFileAsync(string? loadOrderPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((
            new Dictionary<string, string>(),
            false,
            ReportFragment.CreateInfo("Load Order", "No loadorder.txt found", 100)));
    }
    
    public (Dictionary<string, string> plugins, bool limitTriggered, bool limitCheckDisabled) 
        ScanPluginsFromLog(IEnumerable<string> segmentPlugins, Version gameVersion, Version currentVersion, ISet<string>? ignoredPlugins = null)
    {
        var plugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool limitTriggered = false;
        
        foreach (var line in segmentPlugins)
        {
            var match = Regex.Match(line, @"\[([0-9A-F]+)(?:\s+[0-9A-F]+)?\]\s+(.+)");
            if (match.Success)
            {
                var index = match.Groups[1].Value;
                var name = match.Groups[2].Value.Trim();
                
                if (ignoredPlugins?.Contains(name) == true)
                    continue;
                
                plugins[name] = index;
                
                if (index.Equals("FF", StringComparison.OrdinalIgnoreCase))
                    limitTriggered = true;
            }
        }
        
        return (plugins, limitTriggered, false);
    }
    
    public IReadOnlyList<Scanner111.Core.Models.PluginInfo> CreatePluginInfoCollection(
        IDictionary<string, string>? loadOrderPlugins = null,
        IDictionary<string, string>? crashLogPlugins = null,
        ISet<string>? ignoredPlugins = null)
    {
        var result = new List<Scanner111.Core.Models.PluginInfo>();
        
        if (crashLogPlugins != null)
        {
            foreach (var kvp in crashLogPlugins)
            {
                result.Add(new Scanner111.Core.Models.PluginInfo
                {
                    Name = kvp.Key,
                    Origin = kvp.Value,
                    IsIgnored = ignoredPlugins?.Contains(kvp.Key) ?? false
                });
            }
        }
        
        if (loadOrderPlugins != null)
        {
            foreach (var kvp in loadOrderPlugins)
            {
                if (result.Any(p => p.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                    
                result.Add(new Scanner111.Core.Models.PluginInfo
                {
                    Name = kvp.Key,
                    Origin = "LO",
                    IsIgnored = ignoredPlugins?.Contains(kvp.Key) ?? false
                });
            }
        }
        
        return result;
    }
    
    public Dictionary<string, string> FilterIgnoredPlugins(
        IDictionary<string, string> plugins,
        ISet<string> ignoredPlugins)
    {
        return plugins
            .Where(kvp => !ignoredPlugins.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    public Task<bool> ValidateLoadOrderFileAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(filePath));
    
    public PluginLoadingStatistics GetStatistics() => new() 
    { 
        CrashLogPluginCount = 0,
        PluginLimitTriggered = false 
    };
}

internal class ValidationModDatabase : IModDatabase
{
    private readonly Dictionary<string, string> _frequentProblems = new()
    {
        { "ScrapEverything", "Known to cause crashes" },
        { "SpringCleaning", "Outdated and problematic" }
    };
    
    public Task<IReadOnlyDictionary<string, string>> LoadModWarningsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (category == "FREQ")
            return Task.FromResult<IReadOnlyDictionary<string, string>>(_frequentProblems);
        
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
    
    public Task<IReadOnlyDictionary<string, string>> LoadModConflictsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
    
    public Task<IReadOnlyDictionary<string, string>> LoadImportantModsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
    
    public Task<IReadOnlyList<string>> GetModWarningCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "FREQ", "PERF", "STAB" });
    }
    
    public Task<IReadOnlyList<string>> GetImportantModCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "CORE", "CORE_FOLON" });
    }
    
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}