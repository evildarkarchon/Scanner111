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
using System.Text.RegularExpressions;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
///     Settings analyzer tests that use real sample crash logs to verify settings detection.
/// </summary>
public class SettingsAnalyzerSampleDataTests : SampleDataTestBase
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
    [InlineData("crash-2022-06-05-12-52-17.log")]
    [InlineData("crash-2023-09-15-01-54-49.log")]
    public async Task AnalyzeSettings_FromRealCrashLog_ExtractsCorrectSettings(string logFileName)
    {
        // Arrange
        var sampleContent = await ReadSampleLogAsync(logFileName);
        var testLogPath = await CreateTestFileAsync(logFileName, sampleContent);
        
        // Parse settings from the actual log
        var extractedSettings = ExtractSettingsFromLog(sampleContent);
        
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
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        
        // Verify settings were stored in context
        context.TryGetSharedData<CrashGenSettings>("CrashGenSettings", out var storedSettings)
            .Should().BeTrue();
        storedSettings.Should().NotBeNull();
        
        // Check specific settings based on what's in the sample
        if (sampleContent.Contains("MemoryManager: true"))
        {
            storedSettings!.MemoryManager.Should().BeTrue();
        }
        
        if (sampleContent.Contains("Achievements: true"))
        {
            storedSettings!.Achievements.Should().BeFalse("Achievements should be disabled");
        }
    }

    [Fact]
    public async Task AnalyzeSettings_MultipleSampleLogs_ConsistentValidation()
    {
        // Arrange - Test with multiple diverse samples
        var samples = new[]
        {
            "crash-2022-06-05-12-52-17.log",
            "crash-2023-09-15-01-54-49.log",
            "crash-2023-11-08-05-46-35.log"
        };
        
        var results = new Dictionary<string, ReportFragment>();
        
        foreach (var logFileName in samples)
        {
            try
            {
                var sampleContent = await ReadSampleLogAsync(logFileName);
                var testLogPath = await CreateTestFileAsync(logFileName, sampleContent);
                var settings = ExtractSettingsFromLog(sampleContent);
                
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
            catch (FileNotFoundException)
            {
                Logger.LogWarning("Sample file not found: {FileName}", logFileName);
            }
        }
        
        // Assert
        results.Should().NotBeEmpty("At least some samples should be analyzed");
        
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
    }

    [Fact]
    public async Task AnalyzeSettings_WithExpectedOutput_ProducesCompatibleResults()
    {
        // Arrange - Use a sample with known expected output
        var logName = "crash-2023-09-15-01-54-49.log";
        var sampleContent = await ReadSampleLogAsync(logName);
        var expectedOutput = await ReadExpectedOutputAsync(logName);
        
        if (expectedOutput == null)
        {
            Logger.LogWarning("No expected output for validation");
            return;
        }
        
        var testLogPath = await CreateTestFileAsync(logName, sampleContent);
        var settings = ExtractSettingsFromLog(sampleContent);
        
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(testLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert
        result.Should().NotBeNull();
        
        // Check that our validation aligns with expected output
        var fragmentContent = GetFragmentContent(result.Fragment!);
        
        // Expected output mentions specific settings checks
        if (expectedOutput.Contains("Achievements parameter is correctly configured"))
        {
            // Our analyzer should also validate achievements
            context.TryGetSharedData<CrashGenSettings>("CrashGenSettings", out var crashGenSettings)
                .Should().BeTrue();
            
            // Achievements should be false for correct configuration
            if (!crashGenSettings!.Achievements ?? false)
            {
                fragmentContent.Should().MatchEquivalentOf("*Achievements*",
                    "Should mention achievements setting");
            }
        }
        
        if (expectedOutput.Contains("Memory Manager parameter is correctly configured"))
        {
            // Our analyzer should validate memory manager
            if (settings.MemoryManager ?? false)
            {
                fragmentContent.Should().MatchEquivalentOf("*Memory*",
                    "Should mention memory manager setting");
            }
        }
    }

    [Fact]
    public async Task AnalyzeSettings_RealLogWithProblems_DetectsIssues()
    {
        // Arrange - Create a modified sample with known issues
        var originalContent = await ReadSampleLogAsync("crash-2022-06-05-12-52-17.log");
        
        // Modify settings to create problems
        var modifiedContent = originalContent
            .Replace("Achievements: true", "Achievements: false") // This is actually correct
            .Replace("MemoryManager: true", "MemoryManager: false") // This is a problem
            .Replace("ArchiveLimit: ", "ArchiveLimit: true"); // This might be a problem
        
        var testLogPath = await CreateTestFileAsync("problematic.log", modifiedContent);
        var settings = ExtractSettingsFromLog(modifiedContent);
        
        _settingsService.LoadCrashGenSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        
        _settingsService.LoadModDetectionSettingsAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ModDetectionSettings()));
        
        var loggerForAnalyzer = Substitute.For<ILogger<SettingsAnalyzer>>();
        var analyzer = new SettingsAnalyzer(loggerForAnalyzer, _settingsService, _memoryValidator);
        var context = new AnalysisContext(testLogPath, _yamlCore);
        
        // Act
        var result = await analyzer.AnalyzeAsync(context);
        
        // Assert
        result.Should().NotBeNull();
        result.Fragment.Should().NotBeNull();
        
        // Memory manager false should be detected as an issue
        var content = GetFragmentContent(result.Fragment!);
        if (!(settings.MemoryManager ?? false))
        {
            content.Should().MatchEquivalentOf("*Memory*Manager*",
                "Should detect memory manager issue");
        }
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
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = extractedVersion,
            Achievements = achievements,
            MemoryManager = memoryManager,
            ArchiveLimit = archiveLimit,
            F4EE = f4ee
        };
        
        return settings;
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