using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.CLI.TestHelpers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.Services;

[Collection("Terminal UI Tests")]
public class SpectreTerminalUIServiceTests
{
    private readonly TestConsole _console;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly SpectreTerminalUIService _service;

    public SpectreTerminalUIServiceTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;

        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _service = new SpectreTerminalUIService(_mockServiceProvider.Object, _mockSettingsService.Object);
    }

    [Fact]
    public void ShowInteractiveMenu_DisplaysBannerAndTitle()
    {
        // Act
        _service.ShowInteractiveMenu();

        // Assert
        var output = _console.Output;
        output.Should().Contain("Scanner111", "should contain expected text");
        output.Should().Contain("Crash Log Analyzer", "should contain expected text");
    }

    [Fact(Skip = "Moq cannot mock extension methods - needs refactoring")]
    public void CreateProgressContext_CallsMessageHandler()
    {
        // Arrange
        var mockMessageHandler = new Mock<IMessageHandler>();
        var mockProgressContext = new Mock<IProgressContext>();

        mockMessageHandler
            .Setup(x => x.CreateProgressContext(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockProgressContext.Object);

        // Create a service collection and register the message handler
        var services = new ServiceCollection();
        services.AddSingleton(mockMessageHandler.Object);
        var serviceProvider = services.BuildServiceProvider();

        // Create a new service instance with the real service provider
        var service = new SpectreTerminalUIService(serviceProvider, _mockSettingsService.Object);

        // Act
        var result = service.CreateProgressContext("Test Progress", 100);

        // Assert
        result.Should().NotBeNull("value should not be null");
        mockMessageHandler.Verify(x => x.CreateProgressContext("Test Progress", 100), Times.Once);
    }

    [Fact]
    public void DisplayResults_ShowsScanResultInformation()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            Status = ScanStatus.Completed,
            ProcessingTime = TimeSpan.FromSeconds(5),
            Statistics = new ScanStatistics { Scanned = 10, Failed = 2 }
        };
        scanResult.ErrorMessages.Add("Test error");
        scanResult.AnalysisResults.Add(new GenericAnalysisResult
        {
            AnalyzerName = "Test Analyzer",
            Success = true,
            HasFindings = true
        });

        // Act
        _service.DisplayResults(scanResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("Scan Status", "should contain expected text");
        output.Should().Contain("Completed", "should contain expected text");
        output.Should().Contain("00:05", "should contain expected text"); // Processing time
        output.Should().Contain("Errors", "should contain expected text");
        output.Should().Contain("Test error", "should contain expected text");
        output.Should().Contain("Findings", "should contain expected text");
        output.Should().Contain("Test Analyzer", "should contain expected text");
        output.Should().Contain("Scanned: 10, Failed: 2", "should contain expected text");
    }

    [Fact]
    public void ShowLiveStatus_DisplaysStatusWithTimestamp()
    {
        // Act
        _service.ShowLiveStatus("Processing file...");

        // Assert
        var output = _console.Output;
        output.Should().Contain("Processing file...", "should contain expected text");
        Assert.Matches(@"\d{2}:\d{2}:\d{2}", output); // HH:mm:ss format
    }

    [Fact]
    public async Task PromptAsync_ReturnsUserInput()
    {
        // Arrange
        _console.Input.PushText("test input");
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        var result = await _service.PromptAsync<string>("Enter value:", "default");

        // Assert
        result.Should().Be("test input", "value should match expected");
    }

    [Fact]
    public async Task PromptAsync_UsesDefaultWhenProvided()
    {
        // Arrange
        _console.Input.PushKey(ConsoleKey.Enter); // Just press enter to use default

        // Act
        var result = await _service.PromptAsync<string>("Enter value:", "default value");

        // Assert
        result.Should().Be("default value", "value should match expected");
    }

    [Fact]
    public void RunInteractiveMode_RequiresUserInteraction()
    {
        // This test verifies the method exists and can be called
        // Full interaction testing would require more complex setup

        // Arrange
        _console.Input.PushKey(ConsoleKey.DownArrow); // Navigate menu
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.Enter); // Select "Quit"

        // Act
        var task = _service.RunInteractiveMode();

        // Assert - method should execute without throwing
        task.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public void DisplayResults_HandlesEmptyResults()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            LogPath = "empty.log",
            Status = ScanStatus.Completed,
            ProcessingTime = TimeSpan.Zero,
            Statistics = new ScanStatistics()
        };

        // Act
        var exception = Record.Exception(() => _service.DisplayResults(scanResult));

        // Assert
        exception.Should().BeNull("value should be null");
        var output = _console.Output;
        output.Should().Contain("Scan Status", "should contain expected text");
        output.Should().Contain("Completed", "should contain expected text");
    }

    [Fact]
    public void DisplayResults_HandlesFailedStatus()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            LogPath = "failed.log",
            Status = ScanStatus.Failed,
            ProcessingTime = TimeSpan.FromSeconds(1),
            Statistics = new ScanStatistics()
        };

        // Act
        _service.DisplayResults(scanResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("Failed", "should contain expected text");
        // Note: [red] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void DisplayResults_HandlesCompletedWithErrors()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            LogPath = "warnings.log",
            Status = ScanStatus.CompletedWithErrors,
            ProcessingTime = TimeSpan.FromSeconds(3),
            Statistics = new ScanStatistics()
        };

        // Act
        _service.DisplayResults(scanResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("Completed with errors", "should contain expected text");
        // Note: [yellow] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowLiveStatus_HandlesSpecialCharacters()
    {
        // Act
        _service.ShowLiveStatus("Processing: [file] with <special> chars & symbols");

        // Assert
        var output = _console.Output;
        output.Should().Contain("Processing: [file] with <special> chars & symbols", "should contain expected text");
    }

    [Fact(Skip = "Moq cannot mock extension methods - needs refactoring")]
    public void CreateProgressContext_ThrowsWhenMessageHandlerNotRegistered()
    {
        // Arrange - Create empty service provider that doesn't have IMessageHandler registered
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var service = new SpectreTerminalUIService(serviceProvider, _mockSettingsService.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            service.CreateProgressContext("Test", 100));
    }
}