using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
///     Unit tests for the PluginAnalyzer class.
/// </summary>
public class PluginAnalyzerTests
{
    private readonly ILogger<PluginAnalyzer> _logger;
    private readonly IPluginLoader _mockPluginLoader;
    private readonly IAsyncYamlSettingsCore _mockYamlCore;

    public PluginAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<PluginAnalyzer>>();
        _mockPluginLoader = Substitute.For<IPluginLoader>();
        _mockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();

        // Setup default empty configurations
        _mockYamlCore.GetSettingAsync<List<string>>(
                YamlStore.Game, "game_ignore_plugins", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));

        _mockYamlCore.GetSettingAsync<List<string>>(
                YamlStore.Settings, "ignore_list", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));
    }

    [Fact]
    public void Constructor_NullPluginLoader_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginAnalyzer(_logger, null!, _mockYamlCore);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pluginLoader");
    }

    [Fact]
    public void Constructor_NullYamlCore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginAnalyzer(_logger, _mockPluginLoader, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Fact]
    public void Properties_ShouldHaveExpectedValues()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);

        // Assert
        analyzer.Name.Should().Be("PluginAnalyzer");
        analyzer.DisplayName.Should().Be("Plugin Analysis");
        analyzer.Priority.Should().Be(40);
        analyzer.Timeout.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task AnalyzeAsync_WithLoadOrderFile_UsesLoadOrderPlugins()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var loadOrderPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Skyrim.esm", "LO" },
            { "Update.esm", "LO" },
            { "MyMod.esp", "LO" }
        };

        var fragment = ReportFragment.CreateInfo(
            "Load Order Status",
            "Loadorder.txt file found and processed successfully.",
            10);

        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((loadOrderPlugins, true, fragment)));

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics
            {
                LoadOrderPluginCount = 3,
                CrashLogPluginCount = 0,
                IgnoredPluginCount = 0
            });

        _mockPluginLoader.ValidateLoadOrderFileAsync("loadorder.txt")
            .Returns(Task.FromResult(true));

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Metadata.Should().ContainKey("PluginCount");
        result.Metadata["PluginCount"].Should().Be("3");
        result.Metadata.Should().ContainKey("PluginsSource");
        result.Metadata["PluginsSource"].Should().Be("LoadOrder");

        // Verify plugins were stored in context
        context.TryGetSharedData<Dictionary<string, string>>("CrashLogPlugins", out var contextPlugins)
            .Should().BeTrue();
        contextPlugins.Should().HaveCount(3);
        contextPlugins.Should().ContainKey("Skyrim.esm");
    }

    [Fact]
    public async Task AnalyzeAsync_NoLoadOrderFile_ExtractsFromCrashLog()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Setup plugin segment data
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[01] Update.esm",
            "[02] MyMod.esp"
        };
        context.SetSharedData("PluginSegment", pluginSegment);

        // Mock loadorder.txt not found
        var noLoadOrderFragment = ReportFragment.CreateInfo(
            "Load Order Status",
            "No loadorder.txt file found.",
            100);

        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((new Dictionary<string, string>(), false, noLoadOrderFragment)));

        // Mock plugin scanning from log
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Skyrim.esm", "00" },
            { "Update.esm", "01" },
            { "MyMod.esp", "02" }
        };

        _mockPluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((crashLogPlugins, false, false));

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics
            {
                LoadOrderPluginCount = 0,
                CrashLogPluginCount = 3,
                IgnoredPluginCount = 0
            });

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Metadata["PluginCount"].Should().Be("3");
        result.Metadata["PluginsSource"].Should().Be("CrashLog");
    }

    [Fact]
    public async Task AnalyzeAsync_WithCallStackMatches_ReturnsPluginSuspects()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Setup plugin segment data
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[01] SuspiciousMod.esp"
        };
        context.SetSharedData("PluginSegment", pluginSegment);

        // Setup call stack with plugin matches
        var callStackSegment = new List<string>
        {
            "Some crash line mentioning suspiciousmod.esp here",
            "Another line with skyrim.esm reference",
            "suspiciousmod.esp appears again",
            "Unrelated line"
        };
        context.SetSharedData("CallStackSegment", callStackSegment);

        // Mock plugin loading
        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((new Dictionary<string, string>(), false,
                ReportFragment.CreateInfo("Load Order", "No loadorder.txt", 100))));

        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Skyrim.esm", "00" },
            { "SuspiciousMod.esp", "01" }
        };

        _mockPluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((crashLogPlugins, false, false));

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics { CrashLogPluginCount = 2 });

        _mockPluginLoader.ValidateLoadOrderFileAsync("loadorder.txt")
            .Returns(Task.FromResult(false));

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        
        // The result should have children fragments, one for plugin matching
        result.Fragment.Children.Should().NotBeEmpty();
        var pluginSuspectsFragment = result.Fragment.Children
            .FirstOrDefault(c => c.Title.Contains("Plugin Suspects"));
        pluginSuspectsFragment.Should().NotBeNull();
        
        var content = pluginSuspectsFragment!.Content ?? "";
        content.Should().Contain("PLUGINS were found in the CRASH STACK");
        content.Should().Contain("suspiciousmod.esp");
    }

    [Fact]
    public async Task AnalyzeAsync_NoPluginsFound_ReturnsNoPluginsResult()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Setup empty plugin segment
        context.SetSharedData("PluginSegment", new List<string>());

        // Mock no loadorder.txt
        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((new Dictionary<string, string>(), false,
                ReportFragment.CreateInfo("Load Order", "No loadorder.txt", 100))));

        _mockPluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((new Dictionary<string, string>(), false, false));

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics());

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Metadata["PluginCount"].Should().Be("0");
        result.Metadata["PluginsSource"].Should().Be("None");
    }

    [Fact]
    public async Task AnalyzeAsync_PluginLimitTriggered_SetsContextData()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var pluginSegment = new List<string> { "[FF] PluginLimit.esp" };
        context.SetSharedData("PluginSegment", pluginSegment);

        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((new Dictionary<string, string>(), false,
                ReportFragment.CreateInfo("Load Order", "No loadorder.txt", 100))));

        var plugins = new Dictionary<string, string> { { "TestPlugin.esp", "00" } };
        
        _mockPluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((plugins, true, false)); // limitTriggered = true

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics
            {
                PluginLimitTriggered = true,
                CrashLogPluginCount = 1
            });

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Metadata.Should().ContainKey("PluginLimitTriggered");
        result.Metadata["PluginLimitTriggered"].Should().Be("true");

        // Verify context data was set
        context.TryGetSharedData<bool>("PluginLimitTriggered", out var limitTriggered).Should().BeTrue();
        limitTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WithIgnoredPlugins_FiltersCorrectly()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Setup ignored plugins in YAML config
        _mockYamlCore.GetSettingAsync<List<string>>(
                YamlStore.Game, "game_ignore_plugins", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string> { "IgnoredMod.esp" }));

        var pluginSegment = new List<string>
        {
            "[00] GoodMod.esp",
            "[01] IgnoredMod.esp"
        };
        context.SetSharedData("PluginSegment", pluginSegment);

        var callStackSegment = new List<string>
        {
            "crash line with goodmod.esp",
            "crash line with ignoredmod.esp"
        };
        context.SetSharedData("CallStackSegment", callStackSegment);

        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((new Dictionary<string, string>(), false,
                ReportFragment.CreateInfo("Load Order", "No loadorder.txt", 100))));

        var plugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "GoodMod.esp", "00" },
            { "IgnoredMod.esp", "01" }
        };

        _mockPluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((plugins, false, false));

        _mockPluginLoader.GetStatistics()
            .Returns(new PluginLoadingStatistics { CrashLogPluginCount = 2 });

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // The ignored plugin should not appear in plugin suspects
        var content = result.Fragment!.Content ?? 
                     result.Fragment.Children.FirstOrDefault(c => c.Title.Contains("Plugin Suspects"))?.Content ?? "";
        
        if (content.Contains("PLUGINS were found"))
        {
            content.Should().Contain("goodmod.esp");
            content.Should().NotContain("ignoredmod.esp");
        }
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithPluginSegment_ReturnsTrue()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        context.SetSharedData("PluginSegment", new List<string> { "[00] Test.esp" });
        
        _mockPluginLoader.ValidateLoadOrderFileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act
        var canAnalyze = await analyzer.CanAnalyzeAsync(context);

        // Assert
        canAnalyze.Should().BeTrue();
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithValidLoadOrderFile_ReturnsTrue()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // No plugin segment
        _mockPluginLoader.ValidateLoadOrderFileAsync("loadorder.txt")
            .Returns(Task.FromResult(true));

        // Act
        var canAnalyze = await analyzer.CanAnalyzeAsync(context);

        // Assert
        canAnalyze.Should().BeTrue();
    }

    [Fact]
    public async Task CanAnalyzeAsync_NoPluginData_ReturnsFalse()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // No plugin segment and no valid loadorder file
        _mockPluginLoader.ValidateLoadOrderFileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act
        var canAnalyze = await analyzer.CanAnalyzeAsync(context);

        // Assert
        canAnalyze.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_ExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var analyzer = new PluginAnalyzer(_logger, _mockPluginLoader, _mockYamlCore);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Add plugin segment so CanAnalyzeAsync returns true
        context.SetSharedData("PluginSegment", new List<string> { "[00] TestPlugin.esp" });

        _mockPluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Dictionary<string, string>, bool, ReportFragment)>(
                new InvalidOperationException("Test exception")));

        _mockPluginLoader.ValidateLoadOrderFileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Test exception"));
    }
}