using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.Tests.GUI.ViewModels;

public class PapyrusMonitorViewModelTests : IDisposable
{
    private readonly Mock<IAudioNotificationService> _audioServiceMock;
    private readonly Mock<GuiMessageHandlerService> _messageHandlerMock;
    private readonly Mock<IPapyrusMonitorService> _papyrusServiceMock;
    private readonly Mock<IApplicationSettingsService> _settingsServiceMock;
    private readonly PapyrusMonitorViewModel _viewModel;

    public PapyrusMonitorViewModelTests()
    {
        _papyrusServiceMock = new Mock<IPapyrusMonitorService>();
        _settingsServiceMock = new Mock<IApplicationSettingsService>();
        _messageHandlerMock = new Mock<GuiMessageHandlerService>();
        _audioServiceMock = new Mock<IAudioNotificationService>();

        // Setup default settings
        var defaultSettings = new ApplicationSettings
        {
            PapyrusLogPath = "",
            PapyrusMonitorInterval = 1000,
            PapyrusAutoExport = false,
            PapyrusExportPath = ""
        };
        _settingsServiceMock.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(defaultSettings);
        _settingsServiceMock.Setup(x => x.SaveSettingsAsync(It.IsAny<ApplicationSettings>()))
            .Returns(Task.CompletedTask);

        _viewModel = new PapyrusMonitorViewModel(
            _papyrusServiceMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            _audioServiceMock.Object);
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        // Assert
        _viewModel.IsMonitoring.Should().BeFalse();
        _viewModel.CurrentStats.Should().BeNull();
        _viewModel.HasStats.Should().BeFalse();
        _viewModel.History.Should().BeEmpty();
        _viewModel.RecentMessages.Should().BeEmpty();
        _viewModel.StatusMessage.Should().Be("Not monitoring");
        _viewModel.HasAlerts.Should().BeFalse();
        _viewModel.AlertMessage.Should().BeEmpty();
    }

    [Fact]
    public void StartMonitoringCommand_WithValidPath_StartsMonitoring()
    {
        // Arrange
        _viewModel.MonitoredPath = "C:\\test\\papyrus.log";
        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        ((RelayCommand)_viewModel.StartMonitoringCommand).Execute(null);
        Task.Delay(100).Wait(); // Let async operations complete

        // Assert
        _viewModel.IsMonitoring.Should().BeTrue();
        _viewModel.StatusMessage.Should().Contain("Monitoring:");
        _papyrusServiceMock.Verify(x => x.StartMonitoringAsync("C:\\test\\papyrus.log", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void StartMonitoringCommand_WithAutoDetection_DetectsPath()
    {
        // Arrange
        _viewModel.MonitoredPath = null;
        _viewModel.SelectedGameType = GameType.Fallout4;

        _papyrusServiceMock.Setup(x => x.DetectLogPathAsync(GameType.Fallout4))
            .ReturnsAsync("C:\\auto\\detected\\papyrus.log");
        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        ((RelayCommand)_viewModel.StartMonitoringCommand).Execute(null);
        Task.Delay(100).Wait(); // Let async operations complete

        // Assert
        _viewModel.MonitoredPath.Should().Be("C:\\auto\\detected\\papyrus.log");
        _viewModel.IsMonitoring.Should().BeTrue();
        _messageHandlerMock.Verify(x => x.ShowInfo(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Once);
    }

    [Fact]
    public void StartMonitoringCommand_WithNoPathAndNoDetection_ShowsError()
    {
        // Arrange
        _viewModel.MonitoredPath = null;
        _papyrusServiceMock.Setup(x => x.DetectLogPathAsync(It.IsAny<GameType>()))
            .ReturnsAsync((string?)null);

        // Act
        ((RelayCommand)_viewModel.StartMonitoringCommand).Execute(null);
        Task.Delay(100).Wait(); // Let async operations complete

        // Assert
        _viewModel.IsMonitoring.Should().BeFalse();
        _messageHandlerMock.Verify(x => x.ShowError(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Once);
    }

    [Fact]
    public void StopMonitoringCommand_StopsMonitoring()
    {
        // Arrange
        _viewModel.MonitoredPath = "C:\\test\\papyrus.log";
        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _papyrusServiceMock.Setup(x => x.StopMonitoringAsync())
            .Returns(Task.CompletedTask);

        // Start monitoring first
        ((RelayCommand)_viewModel.StartMonitoringCommand).Execute(null);
        Task.Delay(100).Wait();
        _viewModel.IsMonitoring.Should().BeTrue();

        // Act
        ((RelayCommand)_viewModel.StopMonitoringCommand).Execute(null);
        Task.Delay(100).Wait();

        // Assert
        _viewModel.IsMonitoring.Should().BeFalse();
        _viewModel.StatusMessage.Should().Be("Monitoring stopped");
        _papyrusServiceMock.Verify(x => x.StopMonitoringAsync(), Times.Once);
    }

    [Fact]
    public void RefreshCommand_UpdatesStats()
    {
        // Arrange
        _viewModel.MonitoredPath = "C:\\test\\papyrus.log";
        var stats = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0
        };

        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _papyrusServiceMock.Setup(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Start monitoring first
        ((RelayCommand)_viewModel.StartMonitoringCommand).Execute(null);
        Task.Delay(100).Wait();

        // Act
        ((RelayCommand)_viewModel.RefreshCommand).Execute(null);
        Task.Delay(100).Wait();

        // Assert
        _viewModel.CurrentStats.Should().Be(stats);
        _papyrusServiceMock.Verify(x => x.AnalyzeLogAsync("C:\\test\\papyrus.log", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ClearHistoryCommand_ClearsHistory()
    {
        // Arrange
        _viewModel.History.Add(new PapyrusStats { Dumps = 1 });
        _viewModel.History.Add(new PapyrusStats { Dumps = 2 });
        _viewModel.History.Should().HaveCount(2);

        // Act
        ((RelayCommand)_viewModel.ClearHistoryCommand).Execute(null);

        // Assert
        _viewModel.History.Should().BeEmpty();
        _papyrusServiceMock.Verify(x => x.ClearHistory(), Times.Once);
    }

    [Fact]
    public void ExportStatsCommand_ExportsToFile()
    {
        // Arrange
        _viewModel.History.Add(new PapyrusStats { Dumps = 1 });
        _papyrusServiceMock.Setup(x =>
                x.ExportStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        ((RelayCommand)_viewModel.ExportStatsCommand).Execute(null);
        Task.Delay(100).Wait();

        // Assert
        _papyrusServiceMock.Verify(x => x.ExportStatsAsync(It.IsAny<string>(), "csv", It.IsAny<CancellationToken>()),
            Times.Once);
        _messageHandlerMock.Verify(x => x.ShowInfo(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Once);
    }

    [Fact]
    public void PropertyChangedEvents_AreFiredCorrectly()
    {
        // Arrange
        var propertyChangedFired = false;
        string? changedPropertyName = null;

        _viewModel.PropertyChanged += (sender, args) =>
        {
            propertyChangedFired = true;
            changedPropertyName = args.PropertyName;
        };

        // Act
        _viewModel.MonitoredPath = "new/path.log";

        // Assert
        propertyChangedFired.Should().BeTrue();
        changedPropertyName.Should().Be("MonitoredPath");
    }

    [Fact]
    public void CommandCanExecute_UpdatesCorrectly()
    {
        // Initially, start command should be enabled, stop should be disabled
        _viewModel.StartMonitoringCommand.CanExecute(null).Should().BeTrue();
        _viewModel.StopMonitoringCommand.CanExecute(null).Should().BeFalse();
        _viewModel.RefreshCommand.CanExecute(null).Should().BeFalse();

        // When monitoring starts, commands should flip
        _viewModel.GetType().GetProperty("IsMonitoring")!.SetValue(_viewModel, true);

        _viewModel.StartMonitoringCommand.CanExecute(null).Should().BeFalse();
        _viewModel.StopMonitoringCommand.CanExecute(null).Should().BeTrue();
        _viewModel.RefreshCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Act
        _viewModel.Dispose();
        _viewModel.Dispose(); // Should not throw on second call

        // Assert
        _papyrusServiceMock.Verify(x => x.Dispose(), Times.Once);
    }
}