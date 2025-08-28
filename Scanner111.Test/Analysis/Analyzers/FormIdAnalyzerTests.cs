using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Data;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
///     Unit tests for the FormIdAnalyzer class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Analyzer")]
public class FormIdAnalyzerTests
{
    private readonly ILogger<FormIdAnalyzer> _logger;
    private readonly IFormIdDatabase _mockDatabase;
    private readonly IAsyncYamlSettingsCore _mockYamlCore;

    public FormIdAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<FormIdAnalyzer>>();
        _mockDatabase = Substitute.For<IFormIdDatabase>();
        _mockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();
    }

    [Fact]
    public async Task AnalyzeAsync_NoCallStackSegment_ReturnsNoSuspectsMessage()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("COULDN'T FIND ANY FORM ID SUSPECTS");
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyCallStackSegment_ReturnsNoSuspectsMessage()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);
        context.SetSharedData("CallStackSegment", new List<string>());

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("COULDN'T FIND ANY FORM ID SUSPECTS");
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsFormIds_FromCallStack()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase, false);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "   Form ID: 0x00012345",
            "Some other line",
            "  Form ID: 0x00067890",
            "Form ID: 0xFF123456", // Should be skipped (starts with FF)
            "   Form ID: 0x00012345" // Duplicate
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("00012345");
        result.Fragment.Content.Should().Contain("00067890");
        result.Fragment.Content.Should().NotContain("FF123456");
        result.Fragment.Content.Should().Contain("[Skyrim.esm]");
        result.Metadata.Should().ContainKey("FormIdCount");
        result.Metadata["FormIdCount"].Should().Be("3"); // 2 unique + 1 duplicate
    }

    [Fact]
    public async Task AnalyzeAsync_WithDatabaseLookups_RetrievesFormIdDescriptions()
    {
        // Arrange
        _mockDatabase.IsAvailable.Returns(true);
        _mockDatabase.GetEntriesAsync(Arg.Any<(string, string)[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { "Iron Sword", null, "Leather Armor" }));

        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "Form ID: 0x00012345",
            "Form ID: 0x00067890",
            "Form ID: 0x00ABCDEF"
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("Iron Sword");
        result.Fragment.Content.Should().Contain("Leather Armor");
        await _mockDatabase.Received(1).GetEntriesAsync(Arg.Any<(string, string)[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeAsync_DatabaseNotAvailable_SkipsDatabaseLookups()
    {
        // Arrange
        _mockDatabase.IsAvailable.Returns(false);

        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "Form ID: 0x00012345"
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("00012345");
        result.Fragment.Content.Should().Contain("[Skyrim.esm]");
        await _mockDatabase.DidNotReceive()
            .GetEntriesAsync(Arg.Any<(string, string)[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeAsync_DatabaseLookupFails_FallsBackToBasicFormatting()
    {
        // Arrange
        _mockDatabase.IsAvailable.Returns(true);
        _mockDatabase.When(x => x.GetEntriesAsync(Arg.Any<(string, string)[]>(), Arg.Any<CancellationToken>()))
            .Do(x => throw new Exception("Database error"));

        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "Form ID: 0x00012345"
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("00012345");
        result.Fragment.Content.Should().Contain("[Skyrim.esm]");
    }

    [Fact]
    public async Task AnalyzeAsync_CountsFormIdOccurrences()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger, null, false);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "Form ID: 0x00012345",
            "Form ID: 0x00012345",
            "Form ID: 0x00012345",
            "Form ID: 0x00067890"
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("00012345");
        result.Fragment.Content.Should().Contain("| 3"); // Count for 00012345
        result.Fragment.Content.Should().Contain("00067890");
        result.Fragment.Content.Should().Contain("| 1"); // Count for 00067890
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesUnknownPluginPrefix()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger, null, false);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        var callStack = new List<string>
        {
            "Form ID: 0x01234567" // Plugin prefix 01 not in the plugin list
        };

        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" } // Only prefix 00 is known
        };

        context.SetSharedData("CallStackSegment", callStack);
        context.SetSharedData("CrashLogPlugins", plugins);

        // Act
        var result = await analyzer.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment!.Content.Should().Contain("01234567");
        result.Fragment.Content.Should().Contain("[Unknown Plugin 01]");
    }

    [Fact]
    public async Task CanAnalyzeAsync_InitializesDatabase_WhenNotInitialized()
    {
        // Arrange
        _mockDatabase.IsAvailable.Returns(false);
        _mockDatabase.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _mockDatabase.IsAvailable.Returns(true));

        var analyzer = new FormIdAnalyzer(_logger, _mockDatabase);
        var context = new AnalysisContext("test.log", _mockYamlCore);

        // Act
        var canAnalyze = await analyzer.CanAnalyzeAsync(context);

        // Assert
        canAnalyze.Should().BeTrue();
        await _mockDatabase.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger);

        // Assert
        analyzer.Name.Should().Be("FormIdAnalyzer");
    }

    [Fact]
    public void DisplayName_ReturnsCorrectValue()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger);

        // Assert
        analyzer.DisplayName.Should().Be("FormID Analysis");
    }

    [Fact]
    public void Priority_ReturnsCorrectValue()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger);

        // Assert
        analyzer.Priority.Should().Be(60);
    }

    [Fact]
    public void Timeout_ReturnsCorrectValue()
    {
        // Arrange
        var analyzer = new FormIdAnalyzer(_logger);

        // Assert
        analyzer.Timeout.Should().Be(TimeSpan.FromSeconds(45));
    }
}