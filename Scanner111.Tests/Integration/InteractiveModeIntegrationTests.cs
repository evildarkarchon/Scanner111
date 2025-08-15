using Microsoft.Extensions.DependencyInjection;
using Moq;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.CLI.TestHelpers;
using Scanner111.Tests.TestHelpers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.Tests.Integration;

public class InteractiveModeIntegrationTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestMessageCapture _messageCapture;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly ServiceProvider _serviceProvider;

    public InteractiveModeIntegrationTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;

        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);

        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockSettingsService
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings());

        var services = new ServiceCollection();
        ConfigureTestServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        MessageHandler.Initialize(new TestMessageHandler());
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        // Register test implementations
        services.AddSingleton<IMessageHandler>(_messageCapture);
        services.AddSingleton<IApplicationSettingsService>(_mockSettingsService.Object);
        services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();

        // Register mock commands
        services.AddTransient<ICommand<ScanOptions>>(sp => new MockScanCommand());
        services.AddTransient<ICommand<FcxOptions>>(sp => new MockFcxCommand());
        services.AddTransient<ICommand<ConfigOptions>>(sp => new MockConfigCommand());
        services.AddTransient<ICommand<AboutOptions>>(sp => new MockAboutCommand());

        // Register the interactive command
        services.AddTransient<InteractiveCommand>();
    }

    [Fact]
    public void SpectreTerminalUIService_ShowsInteractiveMenu()
    {
        // Arrange
        var uiService = _serviceProvider.GetRequiredService<ITerminalUIService>();

        // Act
        uiService.ShowInteractiveMenu();

        // Assert
        var output = _console.Output;
        Assert.Contains("Scanner111", output);
        Assert.Contains("Crash Log Analyzer", output);
    }

    [Fact]
    public void SpectreTerminalUIService_DisplaysResults()
    {
        // Arrange
        var uiService = _serviceProvider.GetRequiredService<ITerminalUIService>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            Status = ScanStatus.Completed,
            ProcessingTime = TimeSpan.FromSeconds(2.5),
            Statistics = new ScanStatistics { Scanned = 5, Failed = 1 }
        };
        scanResult.AnalysisResults.Add(new GenericAnalysisResult
        {
            AnalyzerName = "Test Analyzer",
            Success = true,
            HasFindings = true,
            ReportLines = new List<string> { "Finding 1", "Finding 2" }
        });

        // Act
        uiService.DisplayResults(scanResult);

        // Assert
        var output = _console.Output;
        Assert.Contains("Scan Status", output);
        Assert.Contains("Completed", output);
        Assert.Contains("Findings", output);
        Assert.Contains("Test Analyzer", output);
    }

    [Fact]
    public void SpectreTerminalUIService_ShowsLiveStatus()
    {
        // Arrange
        var uiService = _serviceProvider.GetRequiredService<ITerminalUIService>();

        // Act
        uiService.ShowLiveStatus("Processing files...");
        uiService.ShowLiveStatus("Analyzing crash logs...");

        // Assert
        var output = _console.Output;
        Assert.Contains("Processing files...", output);
        Assert.Contains("Analyzing crash logs...", output);
    }

    [Fact]
    public void SpectreMessageHandler_WorksWithMessageHandler()
    {
        // Arrange
        var spectreHandler = new SpectreMessageHandler();
        MessageHandler.Initialize(spectreHandler);

        // Act
        MessageHandler.MsgInfo("Test info message");
        MessageHandler.MsgWarning("Test warning");
        MessageHandler.MsgError("Test error");

        // Assert
        var output = _console.Output;
        Assert.Contains("Test info message", output);
        Assert.Contains("Test warning", output);
        Assert.Contains("Test error", output);
    }

    [Fact]
    public void SpectreMessageHandler_ProgressContext_Integration()
    {
        // Arrange
        var spectreHandler = new SpectreMessageHandler();
        MessageHandler.Initialize(spectreHandler);

        // Act
        using (var progress = MessageHandler.CreateProgressContext("Integration Test", 50))
        {
            progress.Update(10, "Processing...");
            progress.Update(25, "Half way...");
            progress.Update(50, "Complete!");
        }

        // Assert - no exceptions thrown
        Assert.True(true);
    }

    [Fact]
    public async Task InteractiveCommand_ExecutesSuccessfully()
    {
        // Arrange
        var command = _serviceProvider.GetRequiredService<InteractiveCommand>();
        var options = new InteractiveOptions { Theme = "default" };

        // Simulate user selecting "Quit"
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        var result = await command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
    }

    // Mock command implementations for testing
    private class MockScanCommand : ICommand<ScanOptions>
    {
        public Task<int> ExecuteAsync(ScanOptions options)
        {
            MessageHandler.MsgInfo("Mock scan executed");
            return Task.FromResult(0);
        }
    }

    private class MockFcxCommand : ICommand<FcxOptions>
    {
        public Task<int> ExecuteAsync(FcxOptions options)
        {
            MessageHandler.MsgInfo("Mock FCX executed");
            return Task.FromResult(0);
        }
    }

    private class MockConfigCommand : ICommand<ConfigOptions>
    {
        public Task<int> ExecuteAsync(ConfigOptions options)
        {
            MessageHandler.MsgInfo("Mock config executed");
            return Task.FromResult(0);
        }
    }

    private class MockAboutCommand : ICommand<AboutOptions>
    {
        public Task<int> ExecuteAsync(AboutOptions options)
        {
            MessageHandler.MsgInfo("Scanner111 Mock About");
            return Task.FromResult(0);
        }
    }
}