using FluentAssertions;
using NSubstitute;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using Scanner111.Test.Infrastructure;
using Scanner111.Test.Infrastructure.Assertions;
using Scanner111.Test.Infrastructure.Mocks;
using Scanner111.Test.Infrastructure.TestBuilders;
using Scanner111.Test.Infrastructure.TestFixtures;

namespace Scanner111.Test.Examples;

/// <summary>
///     Example of refactored PluginAnalyzer tests using new test infrastructure.
///     Demonstrates how to use base classes, builders, and helpers.
/// </summary>
[Collection("TempDirectory")]
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "General")]
public class RefactoredPluginAnalyzerTests : AnalyzerTestBase<PluginAnalyzer>
{
    private readonly IPluginLoader _pluginLoader;
    private readonly TempDirectoryFixture _tempFixture;

    public RefactoredPluginAnalyzerTests(TempDirectoryFixture tempFixture)
    {
        _tempFixture = tempFixture;
        _pluginLoader = MockFactory.CreatePluginLoader();
    }

    protected override PluginAnalyzer CreateAnalyzer()
    {
        return new PluginAnalyzer(Logger, _pluginLoader, MockYamlCore);
    }

    [Fact]
    public void Constructor_ValidatesParameters()
    {
        // Using base class assertion helper
        AssertAnalyzerProperties(
            expectedName: "PluginAnalyzer",
            expectedDisplayName: "Plugin Analysis",
            expectedPriority: 40,
            expectedTimeout: TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task AnalyzeAsync_WithLoadOrderFile_UsesLoadOrderPlugins()
    {
        // Arrange - Using MockFactory for cleaner setup
        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "LO" },
            { "Update.esm", "LO" },
            { "MyMod.esp", "LO" }
        };
        
        var pluginLoader = MockFactory.CreatePluginLoader(
            plugins: plugins,
            hasLoadOrder: true);
        
        var analyzer = new PluginAnalyzer(Logger, pluginLoader, MockYamlCore);
        
        // Using base class helper to add shared data
        WithSharedData("PluginSegment", MockFactory.CreatePluginSegment());

        // Act - Using base class helper
        var result = await analyzer.AnalyzeAsync(TestContext, TestCancellation.Token);

        // Assert - Using base class assertion helper
        AssertSuccessResult(result,
            fragmentAssertion: fragment =>
            {
                // Using custom assertion extensions
                fragment.ShouldHaveBasicProperties(
                    "Plugin Analysis Results",
                    FragmentType.Info);
            },
            metadataAssertion: metadata =>
            {
                metadata.Should().ContainKeys("PluginCount", "PluginsSource");
                metadata["PluginCount"].Should().Be("3");
                metadata["PluginsSource"].Should().Be("LoadOrder");
            });

        // Verify context data
        TestContext.TryGetSharedData<Dictionary<string, string>>("CrashLogPlugins", out var contextPlugins)
            .Should().BeTrue();
        contextPlugins.Should().HaveCount(3);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCallStackMatches_DetectsPluginSuspects()
    {
        // Arrange - Using builders and mock factory
        var suspiciousPlugin = "SuspiciousMod.esp";
        
        WithSharedData("PluginSegment", new List<string>
        {
            "[00] Skyrim.esm",
            $"[01] {suspiciousPlugin}"
        });
        
        WithSharedData("CallStackSegment", new List<string>
        {
            $"Some crash line mentioning {suspiciousPlugin.ToLower()} here",
            "Another line with skyrim.esm reference",
            $"{suspiciousPlugin.ToLower()} appears again"
        });
        
        _pluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((new Dictionary<string, string>
            {
                { "Skyrim.esm", "00" },
                { suspiciousPlugin, "01" }
            }, false, false));

        // Act
        var result = await RunAnalyzerAsync();

        // Assert - Using custom assertion extensions
        result.Fragment.Should().NotBeNull();
        var suspectFragment = result.Fragment!.ShouldHaveChildWithTitle("Plugin Suspects");
        
        suspectFragment.ShouldBeWarning();
        suspectFragment.ShouldContainContent(
            "PLUGINS were found in the CRASH STACK",
            suspiciousPlugin.ToLower());
    }

    [Fact]
    public async Task AnalyzeAsync_WithIgnoredPlugins_FiltersCorrectly()
    {
        // Arrange - Using YAML setting helper from base class
        var ignoredPlugin = "IgnoredMod.esp";
        var goodPlugin = "GoodMod.esp";
        
        WithYamlSetting(YamlStore.Game, "game_ignore_plugins", 
            new List<string> { ignoredPlugin });
        
        WithSharedData("PluginSegment", new List<string>
        {
            $"[00] {goodPlugin}",
            $"[01] {ignoredPlugin}"
        });
        
        WithSharedData("CallStackSegment", new List<string>
        {
            $"crash line with {goodPlugin.ToLower()}",
            $"crash line with {ignoredPlugin.ToLower()}"
        });

        _pluginLoader.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((new Dictionary<string, string>
            {
                { goodPlugin, "00" },
                { ignoredPlugin, "01" }
            }, false, false));

        // Act
        var result = await RunAnalyzerAsync();

        // Assert
        if (result.Fragment?.Children.Any(c => c.Title.Contains("Plugin Suspects")) == true)
        {
            var suspectFragment = result.Fragment.ShouldHaveChildWithTitle("Plugin Suspects");
            suspectFragment.ShouldContainContent(goodPlugin.ToLower());
            suspectFragment.ShouldNotContainContent(ignoredPlugin.ToLower());
        }
    }

    [Theory]
    [InlineData(true, "LoadOrder")]
    [InlineData(false, "CrashLog")]
    public async Task AnalyzeAsync_DifferentPluginSources_SetsCorrectMetadata(
        bool hasLoadOrder,
        string expectedSource)
    {
        // Arrange
        var plugins = new Dictionary<string, string>
        {
            { "Test.esp", hasLoadOrder ? "LO" : "00" }
        };
        
        var pluginLoader = MockFactory.CreatePluginLoader(
            plugins: plugins,
            hasLoadOrder: hasLoadOrder);
            
        var analyzer = new PluginAnalyzer(Logger, pluginLoader, MockYamlCore);
        
        WithSharedData("PluginSegment", MockFactory.CreatePluginSegment("Test.esp"));

        // Act
        var result = await analyzer.AnalyzeAsync(TestContext);

        // Assert
        AssertSuccessResult(result,
            metadataAssertion: metadata =>
            {
                metadata["PluginsSource"].Should().Be(expectedSource);
            });
    }

    [Fact]
    public async Task CanAnalyzeAsync_ValidatesPrerequisites()
    {
        // Arrange & Act & Assert - Using base class helper
        await AssertCanAnalyzeAsync(expected: false);
        
        // Add prerequisites
        WithSharedData("PluginSegment", MockFactory.CreatePluginSegment());
        
        await AssertCanAnalyzeAsync(expected: true);
    }

    [Fact]
    public async Task AnalyzeAsync_WithException_HandlesGracefully()
    {
        // Arrange
        WithSharedData("PluginSegment", MockFactory.CreatePluginSegment());
        
        _pluginLoader.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Dictionary<string, string> plugins, bool pluginsLoaded, ReportFragment fragment)>(
                new InvalidOperationException("Test exception")));

        // Act
        var result = await RunAnalyzerAsync();

        // Assert - Using base class helper
        AssertFailureResult(result, "Test exception");
    }
}