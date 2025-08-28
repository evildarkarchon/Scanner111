using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;

namespace Scanner111.Test.Analysis.Analyzers;

public sealed class SuspectScannerAnalyzerTests
{
    private readonly Mock<ILogger<SuspectScannerAnalyzer>> _loggerMock;
    private readonly SuspectScannerAnalyzer _sut;
    private readonly Mock<IAsyncYamlSettingsCore> _yamlCoreMock;

    public SuspectScannerAnalyzerTests()
    {
        _yamlCoreMock = new Mock<IAsyncYamlSettingsCore>();
        _loggerMock = new Mock<ILogger<SuspectScannerAnalyzer>>();
        _sut = new SuspectScannerAnalyzer(_yamlCoreMock.Object, _loggerMock.Object);

        // Setup default empty configurations
        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, List<string>>());
    }

    [Fact]
    public void Constructor_NullYamlCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SuspectScannerAnalyzer(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SuspectScannerAnalyzer(_yamlCoreMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Properties_ShouldHaveExpectedValues()
    {
        // Assert
        _sut.Name.Should().Be("SuspectScanner");
        _sut.DisplayName.Should().Be("Suspect Pattern Scanner");
        _sut.Priority.Should().Be(20);
        _sut.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_NullContext_ReturnsSkipped()
    {
        // Act
        var result = await _sut.AnalyzeAsync(null!, CancellationToken.None);

        // Assert - base class returns skipped for null context
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Skipped results have Success=true
        result.Warnings.Should().Contain(w => w.Contains("validation failed"));
    }

    [Fact]
    public async Task AnalyzeAsync_NoErrorData_ReturnsSkippedResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.AnalyzerName.Should().Be("SuspectScanner");
        result.Success.Should().BeTrue(); // CreateSkipped returns Success=true
        result.Warnings.Should().Contain(w => w.Contains("skipped") && w.Contains("No crash data available"));
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyErrorStrings_ReturnsSkippedResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "");
        context.SetSharedData("CallStack", "");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // CreateSkipped returns Success=true
        result.Warnings.Should().Contain(w => w.Contains("skipped") && w.Contains("No crash data available"));
    }

    [Fact]
    public async Task AnalyzeAsync_DllCrash_ReturnsWarningResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Error in MyMod.dll at address 0x12345");
        context.SetSharedData("CallStack", "");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Warning);
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("DLL FILE WAS INVOLVED");
        markdown.Should().Contain("prime suspect");
    }

    [Fact]
    public async Task AnalyzeAsync_DllCrashWithTbbmalloc_IgnoresDll()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Error in tbbmalloc.dll at address 0x12345");
        context.SetSharedData("CallStack", "");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().NotContain("DLL FILE WAS INVOLVED");
    }

    [Fact]
    public async Task AnalyzeAsync_MainErrorSuspect_ReturnsErrorResult()
    {
        // Arrange
        var suspectErrors = new Dictionary<string, string>
        {
            ["Critical | Memory Access Violation"] = "ACCESS_VIOLATION",
            ["High | Stack Overflow"] = "STACK_OVERFLOW"
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectErrors);

        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Unhandled exception: ACCESS_VIOLATION reading address 0x00000000");
        context.SetSharedData("CallStack", "");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Error);
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("Memory Access Violation");
        markdown.Should().Contain("SUSPECT FOUND!");
        markdown.Should().Contain("Severity : Critical");
    }

    [Fact]
    public async Task AnalyzeAsync_StackSuspectSimple_FindsMatch()
    {
        // Arrange
        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["High | Problematic Function"] = new() { "BadFunction" }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Generic error");
        context.SetSharedData("CallStack", "at Module.BadFunction()\nat Module.GoodFunction()");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Warning);

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("Problematic Function");
        markdown.Should().Contain("SUSPECT FOUND!");
    }

    [Fact]
    public async Task AnalyzeAsync_StackWithMEREQ_RequiresMainError()
    {
        // Arrange
        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["High | Conditional Crash"] = new()
            {
                "ME-REQ|SPECIFIC_ERROR",
                "BadFunction"
            }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        // Test without required error - should not match
        var context1 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context1.SetSharedData("MainError", "DIFFERENT_ERROR");
        context1.SetSharedData("CallStack", "at Module.BadFunction()");

        var result1 = await _sut.AnalyzeAsync(context1);
        var markdown1 = result1.Fragment!.ToMarkdown();
        markdown1.Should().NotContain("Conditional Crash");

        // Test with required error - should match
        var context2 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context2.SetSharedData("MainError", "SPECIFIC_ERROR occurred");
        context2.SetSharedData("CallStack", "at Module.BadFunction()");

        var result2 = await _sut.AnalyzeAsync(context2);
        var markdown2 = result2.Fragment!.ToMarkdown();
        markdown2.Should().Contain("Conditional Crash");
        markdown2.Should().Contain("SUSPECT FOUND!");
    }

    [Fact]
    public async Task AnalyzeAsync_StackWithMEOPT_OptionalMainError()
    {
        // Arrange
        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["Medium | Optional Error Pattern"] = new()
            {
                "ME-OPT|OPTIONAL_ERROR"
            }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        // With optional error present
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "OPTIONAL_ERROR detected");
        context.SetSharedData("CallStack", "some stack");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("Optional Error Pattern");
        markdown.Should().Contain("SUSPECT FOUND!");
    }

    [Fact]
    public async Task AnalyzeAsync_StackWithNOT_ExcludesWhenFound()
    {
        // Arrange
        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["High | Conditional Pattern"] = new()
            {
                "BadFunction",
                "NOT|SafeFunction"
            }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        // Test with SafeFunction present - should NOT match
        var context1 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context1.SetSharedData("MainError", "Error");
        context1.SetSharedData("CallStack", "at Module.BadFunction()\nat Module.SafeFunction()");

        var result1 = await _sut.AnalyzeAsync(context1);
        var markdown1 = result1.Fragment!.ToMarkdown();
        markdown1.Should().NotContain("Conditional Pattern");

        // Test without SafeFunction - should match
        var context2 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context2.SetSharedData("MainError", "Error");
        context2.SetSharedData("CallStack", "at Module.BadFunction()\nat Module.OtherFunction()");

        var result2 = await _sut.AnalyzeAsync(context2);
        var markdown2 = result2.Fragment!.ToMarkdown();
        markdown2.Should().Contain("Conditional Pattern");
        markdown2.Should().Contain("SUSPECT FOUND!");
    }

    [Fact]
    public async Task AnalyzeAsync_StackWithNumericCount_RequiresMinOccurrences()
    {
        // Arrange
        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["High | Recursive Pattern"] = new()
            {
                "3|RecursiveCall"
            }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        // Test with only 2 occurrences - should not match
        var context1 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context1.SetSharedData("MainError", "Error");
        context1.SetSharedData("CallStack", "RecursiveCall()\nOther()\nRecursiveCall()");

        var result1 = await _sut.AnalyzeAsync(context1);
        var markdown1 = result1.Fragment!.ToMarkdown();
        markdown1.Should().NotContain("Recursive Pattern");

        // Test with 3 occurrences - should match
        var context2 = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context2.SetSharedData("MainError", "Error");
        context2.SetSharedData("CallStack", "RecursiveCall()\nRecursiveCall()\nOther()\nRecursiveCall()");

        var result2 = await _sut.AnalyzeAsync(context2);
        var markdown2 = result2.Fragment!.ToMarkdown();
        markdown2.Should().Contain("Recursive Pattern");
        markdown2.Should().Contain("SUSPECT FOUND!");
    }

    [Fact]
    public async Task AnalyzeAsync_NoSuspectsFound_ReturnsInfoResult()
    {
        // Arrange - empty configurations already set in constructor
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Some random error");
        context.SetSharedData("CallStack", "at Module.Function()");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.None); // Changed from Info to None
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("No known crash suspects detected");
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleSuspects_CombinesAllFindings()
    {
        // Arrange
        var suspectErrors = new Dictionary<string, string>
        {
            ["Critical | Access Violation"] = "ACCESS_VIOLATION"
        };

        var suspectStacks = new Dictionary<string, List<string>>
        {
            ["High | Bad Function"] = new() { "BadFunction" }
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectErrors);

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectStacks);

        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "ACCESS_VIOLATION in Module.dll");
        context.SetSharedData("CallStack", "at Module.BadFunction()");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Error); // Error takes precedence
        result.Fragment.Should().NotBeNull();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("DLL FILE WAS INVOLVED");
        markdown.Should().Contain("Access Violation");
        markdown.Should().Contain("Bad Function");
        markdown.Should().Contain("SUSPECT FOUND!");

        result.Metadata.Should().ContainKey("SuspectCount");
        result.Warnings.Should().Contain(w => w.Contains("Found") && w.Contains("crash suspect"));
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationRequested_ReturnsSkippedResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "Error");
        context.SetSharedData("CallStack", "Stack");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.AnalyzeAsync(context, cts.Token);

        // Assert - the base class catches cancellation and returns a skipped result
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("cancelled"));
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidErrorKeyFormat_LogsWarningAndContinues()
    {
        // Arrange
        var suspectErrors = new Dictionary<string, string>
        {
            ["InvalidKeyWithoutPipe"] = "SOME_ERROR",
            ["Valid | Error Name"] = "VALID_ERROR"
        };

        _yamlCoreMock.Setup(x => x.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectErrors);

        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);
        context.SetSharedData("MainError", "VALID_ERROR occurred");
        context.SetSharedData("CallStack", "");

        // Act
        var result = await _sut.AnalyzeAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var markdown = result.Fragment!.ToMarkdown();
        markdown.Should().Contain("Error Name"); // Valid one should be found
        markdown.Should().NotContain("InvalidKeyWithoutPipe");
    }

    [Fact]
    public async Task CanAnalyzeAsync_ValidContext_ReturnsTrue()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCoreMock.Object);

        // Act
        var result = await _sut.CanAnalyzeAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAnalyzeAsync_NullContext_ReturnsFalse()
    {
        // Act
        var result = await _sut.CanAnalyzeAsync(null!);

        // Assert
        result.Should().BeFalse();
    }
}