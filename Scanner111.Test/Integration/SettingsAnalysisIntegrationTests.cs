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

namespace Scanner111.Test.Integration;

/// <summary>
///     Integration tests for settings analysis components working together.
/// </summary>
public class SettingsAnalysisIntegrationTests
{
    private readonly IAsyncYamlSettingsCore _mockSettingsCore;
    private readonly ServiceProvider _serviceProvider;

    public SettingsAnalysisIntegrationTests()
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configure mocked dependencies
        _mockSettingsCore = Substitute.For<IAsyncYamlSettingsCore>();
        services.AddSingleton(_mockSettingsCore);

        // Register real services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<MemoryManagementValidator>();
        services.AddTransient<SettingsAnalyzer>();
        services.AddSingleton<IFcxModeHandler, FcxModeHandler>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<FcxModeHandler>>();
            var modSettings = new ModDetectionSettings { FcxMode = true };
            return new FcxModeHandler(logger, modSettings);
        });
        services.AddTransient<FcxModeAnalyzer>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task FullAnalysisPipeline_ShouldDetectAllIssues_WithProblematicConfiguration()
    {
        // Arrange - Configure problematic settings
        var yamlData = new Dictionary<string, object?>
        {
            ["Buffout"] = new Dictionary<object, object>
            {
                ["Achievements"] = true, // Problem with achievements.dll
                ["MemoryManager"] = true, // Problem with X-Cell
                ["HavokMemorySystem"] = true, // Also problem with X-Cell
                ["ArchiveLimit"] = true, // Known instability
                ["F4EE"] = false // Problem with f4ee.dll
            },
            ["CrashGenVersion"] = "1.28.0",
            ["XSEPlugins"] = new List<object> { "achievements.dll", "f4ee.dll", "xcell.dll" },
            ["FCXMode"] = true
        };

        _mockSettingsCore.LoadMultipleStoresAsync(Arg.Any<IEnumerable<YamlStore>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<YamlStore, Dictionary<string, object?>>
            {
                [YamlStore.Settings] = yamlData
            }));

        var context = new AnalysisContext(@"C:\test\crashlog.txt", _mockSettingsCore);
        var settingsAnalyzer = _serviceProvider.GetRequiredService<SettingsAnalyzer>();
        var fcxAnalyzer = _serviceProvider.GetRequiredService<FcxModeAnalyzer>();

        // Act - Run both analyzers
        var settingsResult = await settingsAnalyzer.AnalyzeAsync(context);
        var fcxResult = await fcxAnalyzer.AnalyzeAsync(context);

        // Assert - Settings analysis should find issues
        settingsResult.Should().NotBeNull();
        settingsResult.Success.Should().BeTrue();
        settingsResult.Severity.Should().Be(AnalysisSeverity.Warning);
        settingsResult.Fragment.Should().NotBeNull();

        // Check for specific issues in the report
        var markdown = settingsResult.Fragment!.ToMarkdown();
        markdown.Should().Contain("Achievements");
        markdown.Should().Contain("X-Cell");
        markdown.Should().Contain("ArchiveLimit");
        markdown.Should().Contain("Looks Menu");

        // FCX analysis should also succeed
        fcxResult.Should().NotBeNull();
        fcxResult.Success.Should().BeTrue();
        fcxResult.Fragment.Should().NotBeNull();
        fcxResult.Fragment!.Content.Should().Contain("FCX MODE IS ENABLED");
    }

    [Fact]
    public async Task FullAnalysisPipeline_ShouldReportSuccess_WithCorrectConfiguration()
    {
        // Arrange - Configure correct settings
        var yamlData = new Dictionary<string, object?>
        {
            ["Buffout"] = new Dictionary<object, object>
            {
                ["Achievements"] = false,
                ["MemoryManager"] = false, // Disabled because X-Cell is installed
                ["HavokMemorySystem"] = false,
                ["BSTextureStreamerLocalHeap"] = false,
                ["ScaleformAllocator"] = false,
                ["SmallBlockAllocator"] = false,
                ["ArchiveLimit"] = false,
                ["F4EE"] = true
            },
            ["CrashGenVersion"] = "1.30.0",
            ["XSEPlugins"] = new List<object> { "f4ee.dll", "xcell.dll" },
            ["FCXMode"] = false
        };

        _mockSettingsCore.LoadMultipleStoresAsync(Arg.Any<IEnumerable<YamlStore>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<YamlStore, Dictionary<string, object?>>
            {
                [YamlStore.Settings] = yamlData
            }));

        var context = new AnalysisContext(@"C:\test\crashlog.txt", _mockSettingsCore);
        var analyzer = _serviceProvider.GetRequiredService<SettingsAnalyzer>();

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Info);

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("✔️");
        markdown.Should().Contain("correctly configured");
    }

    [Fact]
    public async Task SharedContext_ShouldPropagateData_BetweenAnalyzers()
    {
        // Arrange
        var yamlData = new Dictionary<string, object?>
        {
            ["Buffout"] = new Dictionary<object, object> { ["MemoryManager"] = true },
            ["FCXMode"] = true
        };

        _mockSettingsCore.LoadMultipleStoresAsync(Arg.Any<IEnumerable<YamlStore>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<YamlStore, Dictionary<string, object?>>
            {
                [YamlStore.Settings] = yamlData
            }));

        var context = new AnalysisContext(@"C:\test\crashlog.txt", _mockSettingsCore);
        var settingsAnalyzer = _serviceProvider.GetRequiredService<SettingsAnalyzer>();
        var fcxAnalyzer = _serviceProvider.GetRequiredService<FcxModeAnalyzer>();

        // Act - Run analyzers in sequence
        await settingsAnalyzer.AnalyzeAsync(context);
        await fcxAnalyzer.AnalyzeAsync(context);

        // Assert - Context should contain shared data
        context.TryGetSharedData<CrashGenSettings>("CrashGenSettings", out var crashGenSettings).Should().BeTrue();
        crashGenSettings.Should().NotBeNull();
        crashGenSettings!.CrashGenName.Should().Be("Buffout");

        context.TryGetSharedData<ModDetectionSettings>("ModDetectionSettings", out var modSettings).Should().BeTrue();
        modSettings.Should().NotBeNull();

        context.TryGetSharedData<bool>("FcxChecksCompleted", out var fcxCompleted).Should().BeTrue();
        fcxCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task MemoryManagementValidator_ShouldIntegrate_WithSettingsAnalyzer()
    {
        // Arrange - X-Cell with conflicting settings
        var yamlData = new Dictionary<string, object?>
        {
            ["Buffout"] = new Dictionary<object, object>
            {
                ["MemoryManager"] = true,
                ["HavokMemorySystem"] = true,
                ["BSTextureStreamerLocalHeap"] = true
            },
            ["XSEPlugins"] = new List<object> { "xcell.dll" }
        };

        _mockSettingsCore.LoadMultipleStoresAsync(Arg.Any<IEnumerable<YamlStore>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<YamlStore, Dictionary<string, object?>>
            {
                [YamlStore.Settings] = yamlData
            }));

        var context = new AnalysisContext(@"C:\test\crashlog.txt", _mockSettingsCore);
        var analyzer = _serviceProvider.GetRequiredService<SettingsAnalyzer>();

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("X-Cell is installed");
        markdown.Should().Contain("MemoryManager");
        markdown.Should().Contain("HavokMemorySystem");
        markdown.Should().Contain("BSTextureStreamerLocalHeap");
    }

    [Fact]
    public void ReportFragmentExtensions_ShouldWork_WithAnalyzers()
    {
        // Arrange
        var builder = ReportFragmentBuilder.Create();
        builder.WithTitle("Test Fragment");
        builder.AppendLine("✔️ Test passed successfully");
        builder.AppendLine("# ❌ CAUTION : Test warning detected #");
        builder.AppendLine("* NOTICE : Test notice *");
        builder.WithType(FragmentType.Warning);

        // Act
        var fragment = builder.Build();
        var hasContent = fragment.HasContent();
        var markdown = fragment.ToMarkdown();

        // Assert
        fragment.Should().NotBeNull();
        hasContent.Should().BeTrue();
        markdown.Should().Contain("✔️ Test passed successfully");
        markdown.Should().Contain("CAUTION");
        fragment.Type.Should().Be(FragmentType.Warning); // Due to type set in builder
    }

    [Theory]
    [InlineData(true, true, false, "X-Cell conflict")]
    [InlineData(true, false, true, "Baka ScrapHeap conflict")]
    [InlineData(false, true, true, "X-Cell with Baka")]
    [InlineData(true, false, false, "correctly configured")]
    public async Task VariousConfigurations_ShouldBeDetected(
        bool memManager,
        bool hasXCell,
        bool hasBaka,
        string expectedContent)
    {
        // Arrange
        var yamlData = new Dictionary<string, object?>
        {
            ["Buffout"] = new Dictionary<object, object>
            {
                ["MemoryManager"] = memManager
            }
        };

        if (hasXCell) yamlData["XSEPlugins"] = new List<object> { "xcell.dll" };
        if (hasBaka)
        {
            var existing = yamlData.ContainsKey("XSEPlugins")
                ? (List<object>?)yamlData["XSEPlugins"]
                : null;
            if (existing == null) existing = new List<object>();
            existing.Add("bakascrapheap.dll");
            yamlData["XSEPlugins"] = existing;
        }

        _mockSettingsCore.LoadMultipleStoresAsync(Arg.Any<IEnumerable<YamlStore>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<YamlStore, Dictionary<string, object?>>
            {
                [YamlStore.Settings] = yamlData
            }));

        var context = new AnalysisContext(@"C:\test\crashlog.txt", _mockSettingsCore);
        var analyzer = _serviceProvider.GetRequiredService<SettingsAnalyzer>();

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        var markdown = result.Fragment!.ToMarkdown();

        if (expectedContent.Contains("conflict"))
            markdown.Should().Contain("CAUTION");
        else
            markdown.Should().Contain("✔️");
    }
}