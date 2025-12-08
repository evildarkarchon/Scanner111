using FluentAssertions;
using Moq;
using ReactiveUI;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Orchestration;
using Scanner111.ViewModels;
using Xunit;
using System.Reactive;
using System.Threading.Tasks;
using System.Reactive.Linq; // For extension methods like .FirstAsync()
using Scanner111.Services; // Required for IDialogService

namespace Scanner111.Tests;

public class MainWindowViewModelTests
{
    private readonly Mock<IScanExecutor> _scanExecutorMock;
    private readonly Mock<Func<SettingsViewModel>> _settingsViewModelFactoryMock;
    private readonly Mock<IDialogService> _dialogServiceMock; // Mock the dialog service
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _scanExecutorMock = new Mock<IScanExecutor>();
        _settingsViewModelFactoryMock = new Mock<Func<SettingsViewModel>>();
        _dialogServiceMock = new Mock<IDialogService>(); // Initialize the mock
        
        // Ensure the factory returns a mock or a new instance for consistency
        _settingsViewModelFactoryMock.Setup(f => f()).Returns(() => new SettingsViewModel()); 

        _viewModel = new MainWindowViewModel(
            _scanExecutorMock.Object, 
            _settingsViewModelFactoryMock.Object,
            _dialogServiceMock.Object); // Pass the mock to the ViewModel
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        _viewModel.FcxMode.Should().BeFalse();
        _viewModel.ShowFormIds.Should().BeFalse();
        _viewModel.StatusText.Should().Be("Ready");
        _viewModel.Progress.Should().Be(0);
        _viewModel.IsScanning.Should().BeFalse();
        _viewModel.ScanResults.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanCommand_ExecutesScan_UpdatesProgressAndStatus()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            Statistics = new ScanStatistics { Scanned = 2, Failed = 0, TotalFiles = 2 },
            ProcessedFiles = new[] { "log1.log", "log2.log" },
            ScanDuration = TimeSpan.FromSeconds(1.5)
        };

        _scanExecutorMock.Setup(x => x.ExecuteScanAsync(It.IsAny<ScanConfig>(), It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<ScanConfig, IProgress<ScanProgress>, CancellationToken>(async (config, progress, ct) =>
            {
                progress.Report(new ScanProgress { FilesProcessed = 1, TotalFiles = 2, CurrentFile = "log1.log", Statistics = new ScanStatistics() });
                await Task.Delay(50, ct); // Simulate work
                progress.Report(new ScanProgress { FilesProcessed = 2, TotalFiles = 2, CurrentFile = "log2.log", Statistics = new ScanStatistics() });
                return scanResult;
            });

        // Act
        var commandExecution = _viewModel.ScanCommand.Execute();
        await commandExecution.FirstAsync(); // Wait for the command to finish executing

        // Assert
        _viewModel.IsScanning.Should().BeFalse();
        _viewModel.StatusText.Should().Contain("Complete");
        _viewModel.ScanResults.Should().HaveCount(2);
        _viewModel.ScanResults.Should().Contain(r => r.FileName == "log1.log" && r.Status == "Completed");
        _viewModel.ScanResults.Should().Contain(r => r.FileName == "log2.log" && r.Status == "Completed");
    }

    [Fact]
    public async Task ScanCommand_HandlesErrors_UpdatesStatus()
    {
        // Arrange
        var errorMessage = "Simulated scan error";
        _scanExecutorMock.Setup(x => x.ExecuteScanAsync(It.IsAny<ScanConfig>(), It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        // Act
        var commandExecution = _viewModel.ScanCommand.Execute();
        await commandExecution.FirstAsync(); // Wait for the command to finish executing

        // Assert
        _viewModel.IsScanning.Should().BeFalse();
        _viewModel.StatusText.Should().Contain($"Error: {errorMessage}");
        _viewModel.ScanResults.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenSettingsCommand_OpensSettingsDialog()
    {
        // Arrange
        // The factory will be called by the ViewModel's OpenSettings method.
        // We set up the dialog service to complete without error.
        _dialogServiceMock.Setup(x => x.ShowSettingsDialogAsync(It.IsAny<SettingsViewModel>()))
            .Returns(Task.CompletedTask); 

        // Act
        await _viewModel.OpenSettingsCommand.Execute();

        // Assert
        _settingsViewModelFactoryMock.Verify(f => f(), Times.Once); // Factory should be called once by the ViewModel
        _dialogServiceMock.Verify(x => x.ShowSettingsDialogAsync(It.IsAny<SettingsViewModel>()), Times.Once); // Dialog service should be called once
        _viewModel.StatusText.Should().Be("Settings dialog closed");
    }
}