using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using Scanner111.Test.Infrastructure;
using Scanner111.Test.Infrastructure.TestData;
using System.Text.RegularExpressions;
using VerifyXunit;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
///     Migrated settings analyzer tests using embedded resources and snapshot testing.
///     Demonstrates the new self-contained testing approach.
/// </summary>
public class SettingsAnalyzerMigratedTests : EmbeddedResourceTestBase
{
    private ISettingsService _settingsService = null!;
    private MemoryManagementValidator _memoryValidator = null!;
    private IAsyncYamlSettingsCore _yamlCore = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _settingsService = Substitute.For<ISettingsService>();
        
        services.AddSingleton(_yamlCore);
        services.AddSingleton(_settingsService);
        services.AddTransient<MemoryManagementValidator>();
        services.AddTransient<SettingsAnalyzer>();
    }

    protected override Task OnInitializeAsync()
    {
        _memoryValidator = GetService<MemoryManagementValidator>();
        return Task.CompletedTask;
    }

    [Theory]
    [ClassData(typeof(EmbeddedLogTheoryData))]
    public async Task AnalyzeSettings_FromEmbeddedLog_ExtractsCorrectSettings(string logFileName)
    {
        // Arrange - Using embedded resource instead of sample files
        var logContent = await GetEmbeddedLogAsync(logFileName);
        var testLogPath = await CreateTestFileFromEmbeddedAsync(logFileName);
        
        // Parse settings from the actual log
        var extractedSettings = ExtractSettingsFromLog(logContent);
        
        // Setup mock to return these settings
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(extractedSettings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(testLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert using FluentAssertions
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        
        // Verify settings were stored in context
        context.TryGetSharedData<CrashGenSettings>("CrashGenSettings", out var storedSettings)
            .Should().BeTrue();
        storedSettings.Should().NotBeNull();
        
        // Verify snapshot of the result
        await VerifyReportFragmentAsync(result.Fragment!);
    }

    [Theory]
    [ClassData(typeof(SyntheticScenarioTheoryData))]
    public async Task AnalyzeSettings_WithSyntheticData_ProducesExpectedResults(int seed, CrashLogOptions options)
    {
        // Arrange - Using synthetic data generation
        var crashLogPath = await CreateSyntheticCrashLogFileAsync($"synthetic_{seed}.log", options);
        
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(options.Settings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(DataGenerator.GenerateModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(crashLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // Snapshot test with parameterized settings
        var verifySettings = CreateParameterizedSettings(seed, options.ErrorType);
        await VerifyWithSettingsAsync(result.Fragment, verifySettings);
    }

    [Fact]
    public async Task AnalyzeSettings_MultipleEmbeddedLogs_ConsistentValidation()
    {
        // Arrange - Test with multiple diverse embedded resources
        var samples = new[]
        {
            CriticalSampleLogs.EarlySample,
            CriticalSampleLogs.WithPluginIssues,
            CriticalSampleLogs.WithMemoryIssues
        };
        
        var results = new Dictionary<string, ReportFragment>();
        
        foreach (var logFileName in samples)
        {
            var logContent = await GetEmbeddedLogAsync(logFileName);
            var testLogPath = await CreateTestFileFromEmbeddedAsync(logFileName);
            var settings = ExtractSettingsFromLog(logContent);
            
            _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(settings));
            
            _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new ModDetectionSettings()));
            
            var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
            var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
            var context = new AnalysisContext(testLogPath, _yamlCore);
            
            // Act
            var result = await analyzer.AnalyzeAsync(context);
            
            if (result.Fragment != null)
            {
                results[logFileName] = result.Fragment;
            }
        }
        
        // Assert
        results.Should().HaveCount(samples.Length);
        
        // All results should have consistent structure
        foreach (var (fileName, fragment) in results)
        {
            fragment.Title.Should().Contain("Settings",
                $"Settings fragment for {fileName} should have appropriate title");
            
            // Check for common validation messages
            var content = GetFragmentContent(fragment);
            content.Should().ContainAny(
                "correctly configured",
                "misconfigured",
                "recommended",
                "warning");
        }
        
        // Snapshot test for all results
        await VerifyCollectionAsync(results);
    }

    [Fact]
    public async Task AnalyzeSettings_HybridTestData_CombinesRealAndSynthetic()
    {
        // Arrange - Hybrid approach: real base with synthetic modifications
        var hybridLogPath = await CreateHybridTestFileAsync(
            CriticalSampleLogs.WithPluginIssues,
            options =>
            {
                // Modify settings to introduce problems
                options.Settings = new CrashGenSettings
                {
                    CrashGenName = "Buffout",
                    Version = options.Settings.Version,
                    MemoryManager = false,
                    Achievements = true,
                    ArchiveLimit = options.Settings.ArchiveLimit,
                    F4EE = options.Settings.F4EE
                };
                options.ErrorType = "EXCEPTION_STACK_OVERFLOW";
            });
        
        var logContent = await File.ReadAllTextAsync(hybridLogPath);
        var settings = ExtractSettingsFromLog(logContent);
        
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(hybridLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert
        result.Should().NotBeNull();
        result.Fragment.Should().NotBeNull();
        
        // Should detect the introduced problems
        var content = GetFragmentContent(result.Fragment!);
        
        if (!(settings.MemoryManager ?? false))
        {
            content.Should().MatchEquivalentOf("*Memory*Manager*",
                "Should detect memory manager issue");
        }
        
        if (settings.Achievements ?? false)
        {
            content.Should().MatchEquivalentOf("*Achievements*",
                "Should detect achievements issue");
        }
        
        // Snapshot verification
        await VerifyAsync(new
        {
            Settings = settings,
            Fragment = result.Fragment
        });
    }

    [Fact]
    public async Task AnalyzeSettings_DeterministicSyntheticData_ProducesConsistentResults()
    {
        // Arrange - Using deterministic synthetic data for reproducible tests
        const int seed = 42;
        var options = new CrashLogOptions
        {
            Settings = DataGenerator.GenerateCrashGenSettings(valid: false)
        };
        
        // Generate two identical logs with the same seed
        var crashLog1 = GenerateDeterministicCrashLog(seed, options);
        var crashLog2 = GenerateDeterministicCrashLog(seed, options);
        
        // Assert logs are identical
        crashLog1.Should().Be(crashLog2, "Deterministic generation should produce identical results");
        
        var testLogPath = await CreateTestFileAsync("deterministic.log", crashLog1);
        
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(options.Settings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(testLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert
        result.Should().NotBeNull();
        
        // Verify snapshot
        await VerifyAsync(result.Fragment);
    }

    private CrashGenSettings ExtractSettingsFromLog(string logContent)
    {
        // Pre-extract all settings values
        Version? extractedVersion = null;
        bool? achievements = null;
        bool? memoryManager = null;
        bool? archiveLimit = null;
        bool? f4ee = null;
        
        // Extract version
        var versionMatch = Regex.Match(logContent, @"Buffout 4 v([\d\.]+)");
        if (versionMatch.Success)
        {
            Version.TryParse(versionMatch.Groups[1].Value, out extractedVersion);
        }
        
        // Extract settings values
        var lines = logContent.Split('\n');
        var inSettingsSection = false;
        
        foreach (var line in lines)
        {
            if (line.Trim() == "SETTINGS:")
            {
                inSettingsSection = true;
                continue;
            }
            
            if (inSettingsSection)
            {
                if (line.Contains("Achievements:"))
                {
                    achievements = line.Contains("true");
                }
                else if (line.Contains("MemoryManager:"))
                {
                    memoryManager = line.Contains("true");
                }
                else if (line.Contains("ArchiveLimit:"))
                {
                    var value = ExtractSettingValue(line);
                    if (int.TryParse(value, out var limit))
                    {
                        archiveLimit = limit > 0;
                    }
                    else
                    {
                        archiveLimit = value?.ToLower() == "true";
                    }
                }
                else if (line.Contains("F4EE:"))
                {
                    f4ee = line.Contains("true");
                }
                else if (!line.StartsWith("\t") && !line.StartsWith("    ") && !string.IsNullOrWhiteSpace(line))
                {
                    // End of settings section
                    break;
                }
            }
        }
        
        // Create settings with all extracted values
        return new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = extractedVersion,
            Achievements = achievements,
            MemoryManager = memoryManager,
            ArchiveLimit = archiveLimit,
            F4EE = f4ee
        };
    }

    private string? ExtractSettingValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < line.Length - 1)
        {
            return line.Substring(colonIndex + 1).Trim();
        }
        return null;
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
}