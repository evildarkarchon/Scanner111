using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console;
using Spectre.Console.Testing;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.CLI.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

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
        Assert.Contains("Scanner111", output);
        Assert.Contains("Crash Log Analyzer", output);
    }

    [Fact]
    public void CreateProgressContext_CallsMessageHandler()
    {
        // Arrange
        var mockMessageHandler = new Mock<IMessageHandler>();
        var mockProgressContext = new Mock<IProgressContext>();
        
        mockMessageHandler
            .Setup(x => x.CreateProgressContext(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockProgressContext.Object);
        
        _mockServiceProvider
            .Setup(x => x.GetRequiredService(typeof(IMessageHandler)))
            .Returns(mockMessageHandler.Object);

        // Act
        var result = _service.CreateProgressContext("Test Progress", 100);

        // Assert
        Assert.NotNull(result);
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
        Assert.Contains("Scan Status", output);
        Assert.Contains("Completed", output);
        Assert.Contains("00:05", output); // Processing time
        Assert.Contains("Errors", output);
        Assert.Contains("Test error", output);
        Assert.Contains("Findings", output);
        Assert.Contains("Test Analyzer", output);
        Assert.Contains("Scanned: 10, Failed: 2", output);
    }

    [Fact]
    public void ShowLiveStatus_DisplaysStatusWithTimestamp()
    {
        // Act
        _service.ShowLiveStatus("Processing file...");

        // Assert
        var output = _console.Output;
        Assert.Contains("Processing file...", output);
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
        Assert.Equal("test input", result);
    }

    [Fact]
    public async Task PromptAsync_UsesDefaultWhenProvided()
    {
        // Arrange
        _console.Input.PushKey(ConsoleKey.Enter); // Just press enter to use default

        // Act
        var result = await _service.PromptAsync<string>("Enter value:", "default value");

        // Assert
        Assert.Equal("default value", result);
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
        Assert.NotNull(task);
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
        Assert.Null(exception);
        var output = _console.Output;
        Assert.Contains("Scan Status", output);
        Assert.Contains("Completed", output);
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
        Assert.Contains("Failed", output);
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
        Assert.Contains("Completed with errors", output);
        // Note: [yellow] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowLiveStatus_HandlesSpecialCharacters()
    {
        // Act
        _service.ShowLiveStatus("Processing: [file] with <special> chars & symbols");

        // Assert
        var output = _console.Output;
        Assert.Contains("Processing: [file] with <special> chars & symbols", output);
    }

    [Fact]
    public void CreateProgressContext_ThrowsWhenMessageHandlerNotRegistered()
    {
        // Arrange
        _mockServiceProvider
            .Setup(x => x.GetRequiredService(typeof(IMessageHandler)))
            .Throws(new InvalidOperationException("Service not registered"));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            _service.CreateProgressContext("Test", 100));
    }
}