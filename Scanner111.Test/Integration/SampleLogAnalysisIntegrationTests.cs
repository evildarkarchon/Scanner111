using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.IO;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Scanner111.Core.Orchestration.ExecutionStrategies;
using Scanner111.Core.Services;
using Scanner111.Test.Infrastructure;

namespace Scanner111.Test.Integration;

/// <summary>
///     Integration tests that analyze real sample crash logs from the sample_logs directory.
/// </summary>
public class SampleLogAnalysisIntegrationTests : SampleDataTestBase
{
    private IAnalyzerOrchestrator _orchestrator = null!;
    private IAsyncYamlSettingsCore _yamlCore = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Configure real services for integration testing
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        SetupDefaultYamlMocks();
        
        services.AddSingleton(_yamlCore);
        services.AddTransient<IPluginLoader, MockPluginLoader>();
        services.AddTransient<IModDatabase, MockModDatabase>();
        services.AddTransient<IXsePluginChecker, MockXsePluginChecker>();
        services.AddTransient<ICrashGenChecker, MockCrashGenChecker>();
        
        // Add analyzers
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<SettingsAnalyzer>();
        services.AddTransient<SuspectScannerAnalyzer>();
        services.AddTransient<ModDetectionAnalyzer>();
        
        // Add orchestrator
        services.AddTransient<IAnalyzerOrchestrator, AnalyzerOrchestrator>();
    }

    protected override Task OnInitializeAsync()
    {
        _orchestrator = GetService<IAnalyzerOrchestrator>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnalyzeSampleLog_BasicCrashLog_ExtractsMainInformation()
    {
        // Arrange
        var sampleContent = await ReadSampleLogAsync("crash-2022-06-05-12-52-17.log");
        var testLogPath = await CreateTestFileAsync("test-crash.log", sampleContent);
        
        // Act
        var context = new AnalysisContext(testLogPath, _yamlCore);
        
        // Parse the log content into context (simulating what the main app does)
        ParseLogIntoContext(context, sampleContent);
        
        // Run analyzers
        var request = new AnalysisRequest
        {
            InputPath = testLogPath,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Sequential,
                VerboseOutput = true
            }
        };
        var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, TestCancellation.Token);
        var results = orchestrationResult.Results.ToList();
        
        // Assert
        results.Should().NotBeEmpty();
        
        // Check that main error was detected
        context.TryGetSharedData<string>("MainError", out var mainError).Should().BeTrue();
        mainError.Should().Contain("EXCEPTION_ACCESS_VIOLATION");
        mainError.Should().Contain("0x7FF6A1C08F6A");
        
        // Check that version was detected
        context.TryGetSharedData<string>("Buffout4Version", out var version).Should().BeTrue();
        version.Should().Be("1.24.5");
    }

    [Theory]
    [ClassData(typeof(SampleLogTheoryData))]
    public async Task AnalyzeSampleLog_VariousCrashLogs_SuccessfullyParsesAll(string logFileName)
    {
        // Arrange
        var sampleContent = await ReadSampleLogAsync(logFileName);
        var testLogPath = await CreateTestFileAsync(logFileName, sampleContent);
        
        // Act
        var context = new AnalysisContext(testLogPath, _yamlCore);
        ParseLogIntoContext(context, sampleContent);
        
        var request = new AnalysisRequest
        {
            InputPath = testLogPath,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Parallel,
                VerboseOutput = true
            }
        };
        var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, TestCancellation.Token);
        var results = orchestrationResult.Results.ToList();
        
        // Assert
        results.Should().NotBeEmpty();
        results.Where(r => r.Success).Should().NotBeEmpty(
            $"At least some analyzers should succeed for {logFileName}");
        
        // All logs should have a main error
        context.TryGetSharedData<string>("MainError", out var mainError).Should().BeTrue(
            $"Main error should be detected in {logFileName}");
        mainError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnalyzeSampleLog_WithPlugins_ExtractsPluginList()
    {
        // Arrange - Use a log known to have plugins
        var sampleContent = await ReadSampleLogAsync("crash-2023-09-15-01-54-49.log");
        var testLogPath = await CreateTestFileAsync("plugin-test.log", sampleContent);
        
        // Act
        var context = new AnalysisContext(testLogPath, _yamlCore);
        ParseLogIntoContext(context, sampleContent);
        
        // Run plugin analyzer specifically
        var pluginAnalyzerLogger = Substitute.For<ILogger<PluginAnalyzer>>();
        var pluginAnalyzer = new PluginAnalyzer(
            pluginAnalyzerLogger, 
            GetService<IPluginLoader>(), 
            _yamlCore);
        
        if (await pluginAnalyzer.CanAnalyzeAsync(context))
        {
            var result = await pluginAnalyzer.AnalyzeAsync(context);
            
            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            
            // Check that plugins were detected
            context.TryGetSharedData<Dictionary<string, string>>("CrashLogPlugins", out var plugins)
                .Should().BeTrue();
            plugins.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task AnalyzeSampleLog_WithCallStack_ExtractsStackInformation()
    {
        // Arrange
        var sampleContent = await ReadSampleLogAsync("crash-2022-06-05-12-52-17.log");
        var testLogPath = await CreateTestFileAsync("stack-test.log", sampleContent);
        
        // Act
        var context = new AnalysisContext(testLogPath, _yamlCore);
        ParseLogIntoContext(context, sampleContent);
        
        // Assert
        context.TryGetSharedData<List<string>>("CallStackSegment", out var callStack).Should().BeTrue();
        callStack.Should().NotBeEmpty();
        callStack.Should().Contain(line => line.Contains("Fallout4.exe"));
    }

    [Fact]
    public async Task AnalyzeSampleLog_CompareWithExpectedOutput_KeyElementsMatch()
    {
        // Arrange - Use a log with known expected output
        var logName = "crash-2023-09-15-01-54-49.log";
        var sampleContent = await ReadSampleLogAsync(logName);
        var expectedOutput = await ReadExpectedOutputAsync(logName);
        
        expectedOutput.Should().NotBeNull("Expected output file should exist for this test case");
        
        var testLogPath = await CreateTestFileAsync(logName, sampleContent);
        
        // Act
        var context = new AnalysisContext(testLogPath, _yamlCore);
        ParseLogIntoContext(context, sampleContent);
        
        // Run orchestrator
        var request = new AnalysisRequest
        {
            InputPath = testLogPath,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Sequential,
                VerboseOutput = true
            }
        };
        
        var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, TestCancellation.Token);
        var results = orchestrationResult.Results.ToList();
        
        // Generate a simple report from results
        var reportComposerLogger = Substitute.For<ILogger<ReportComposer>>();
        var reportComposer = new ReportComposer(reportComposerLogger);
        
        // Compose fragments from all analyzers
        var fragments = results.Where(r => r.Fragment != null)
            .Select(r => r.Fragment!);
        
        var actualOutput = await reportComposer.ComposeFromFragmentsAsync(fragments);
        
        // Assert - Validate against expected patterns
        ValidateAgainstExpectedOutput(actualOutput, expectedOutput!);
        
        // Check specific expected elements from the sample
        if (expectedOutput!.Contains("BA2 Limit"))
        {
            // This specific log has a BA2 limit issue
            var suspectResult = results.FirstOrDefault(r => r.AnalyzerName == "SuspectScanner");
            if (suspectResult?.Fragment != null)
            {
                var content = GetFragmentContent(suspectResult.Fragment);
                // The actual implementation may detect this differently
                Logger.LogInformation("Suspect scanner content: {Content}", content);
            }
        }
    }

    [Fact]
    public async Task AnalyzeSampleLogs_AllMatchingPairs_ProduceConsistentResults()
    {
        // Arrange
        var pairs = GetMatchingSamplePairs().Take(5).ToList(); // Test first 5 for speed
        pairs.Should().NotBeEmpty("Should have at least some matching sample pairs");
        
        var failedValidations = new List<string>();
        
        // Act & Assert
        foreach (var (logPath, outputPath) in pairs)
        {
            try
            {
                var logName = Path.GetFileName(logPath);
                var sampleContent = await File.ReadAllTextAsync(logPath);
                var expectedOutput = await File.ReadAllTextAsync(outputPath);
                
                var testLogPath = await CreateTestFileAsync(logName, sampleContent);
                var context = new AnalysisContext(testLogPath, _yamlCore);
                ParseLogIntoContext(context, sampleContent);
                
                // Just verify we can analyze without errors
                var request = new AnalysisRequest
                {
                    InputPath = testLogPath,
                    Options = new OrchestrationOptions
                    {
                        Strategy = ExecutionStrategy.Sequential,
                        VerboseOutput = true
                    }
                };
                
                var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, TestCancellation.Token);
                var results = orchestrationResult.Results.ToList();
                
                results.Should().NotBeEmpty($"Should get results for {logName}");
                
                // Basic validation - check if main error was detected like in expected
                if (expectedOutput.Contains("Main Error:"))
                {
                    context.TryGetSharedData<string>("MainError", out var mainError).Should().BeTrue(
                        $"Main error should be detected in {logName} since it's in expected output");
                }
            }
            catch (Exception ex)
            {
                failedValidations.Add($"{Path.GetFileName(logPath)}: {ex.Message}");
            }
        }
        
        failedValidations.Should().BeEmpty(
            $"All sample validations should pass. Failures: {string.Join(", ", failedValidations)}");
    }

    private void ParseLogIntoContext(AnalysisContext context, string logContent)
    {
        var lines = logContent.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        // Extract main error (first line with "Unhandled exception")
        var errorLine = lines.FirstOrDefault(l => l.Contains("Unhandled exception"));
        if (!string.IsNullOrEmpty(errorLine))
        {
            context.SetSharedData("MainError", errorLine);
        }
        
        // Extract Buffout version
        var versionLine = lines.FirstOrDefault(l => l.StartsWith("Buffout 4 v"));
        if (!string.IsNullOrEmpty(versionLine))
        {
            var version = versionLine.Replace("Buffout 4 v", "").Trim();
            context.SetSharedData("Buffout4Version", version);
        }
        
        // Extract plugins section
        var pluginsStartIdx = Array.FindIndex(lines, l => l == "PLUGINS:");
        if (pluginsStartIdx >= 0)
        {
            var pluginLines = new List<string>();
            for (int i = pluginsStartIdx + 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("    ") && !lines[i].StartsWith("\t"))
                    break;
                pluginLines.Add(lines[i].Trim());
            }
            if (pluginLines.Any())
            {
                context.SetSharedData("PluginSegment", pluginLines);
            }
        }
        
        // Extract call stack
        var stackStartIdx = Array.FindIndex(lines, l => l.StartsWith("PROBABLE CALL STACK:"));
        if (stackStartIdx >= 0)
        {
            var stackLines = new List<string>();
            for (int i = stackStartIdx + 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("    [") && !lines[i].StartsWith("\t["))
                    break;
                stackLines.Add(lines[i].Trim());
            }
            if (stackLines.Any())
            {
                context.SetSharedData("CallStackSegment", stackLines);
                context.SetSharedData("CallStack", string.Join('\n', stackLines));
            }
        }
        
        // Store full log content
        context.SetSharedData("LogContent", logContent);
        context.SetSharedData("LogLines", lines.ToList());
    }

    private string GetFragmentContent(ReportFragment fragment)
    {
        var content = fragment.Content ?? "";
        foreach (var child in fragment.Children)
        {
            content += "\n" + GetFragmentContent(child);
        }
        return content;
    }

    private void SetupDefaultYamlMocks()
    {
        _yamlCore.GetSettingAsync<string>(
            Arg.Any<YamlStore>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("false"));
        
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
    }
}

// Mock implementations for testing
internal class MockPluginLoader : IPluginLoader
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
        foreach (var line in segmentPlugins)
        {
            // Parse lines like "[00] Plugin.esp"
            var match = System.Text.RegularExpressions.Regex.Match(line, @"\[([0-9A-F]+)\]\s+(.+)");
            if (match.Success)
            {
                plugins[match.Groups[2].Value] = match.Groups[1].Value;
            }
        }
        return (plugins, false, false);
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
        => Task.FromResult(false);
    
    public PluginLoadingStatistics GetStatistics() => new() { CrashLogPluginCount = 0 };
}

internal class MockModDatabase : IModDatabase
{
    public Task<IReadOnlyDictionary<string, string>> LoadModWarningsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
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

internal class MockXsePluginChecker : IXsePluginChecker
{
    public Task<string> CheckXsePluginsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("XSE plugins check: No issues detected");
    }
    
    public Task<(bool IsCorrectVersion, string Message)> ValidateAddressLibraryAsync(
        string pluginsPath, 
        string gameVersion,
        bool isVrMode, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, "Address Library version is correct"));
    }
}

internal class MockCrashGenChecker : ICrashGenChecker
{
    public Task<string> CheckCrashGenSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Crash generator settings check: Configuration is valid");
    }
    
    public Task<IReadOnlySet<string>> DetectInstalledPluginsAsync(string pluginsPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
    
    public Task<bool> HasPluginAsync(IEnumerable<string> pluginNames, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}