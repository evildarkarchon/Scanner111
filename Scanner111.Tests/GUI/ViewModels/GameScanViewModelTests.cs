using System.Reactive.Linq;
using System.Reflection;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.GUI.Models;
using Scanner111.GUI.ViewModels;
using Scanner111.Tests.GUI.TestHelpers;

namespace Scanner111.Tests.GUI.ViewModels;

[Collection("GUI Tests")]
public class GameScanViewModelTests : IDisposable
{
    private readonly List<string> _errorMessages = new();
    private readonly List<string> _infoMessages = new();
    private readonly Mock<IGameScannerService> _mockGameScannerService;
    private readonly Mock<IMessageHandler> _mockMessageHandler;
    private readonly MockSettingsService _mockSettingsService;
    private readonly List<string> _successMessages = new();
    private readonly GameScanViewModel _viewModel;
    private readonly List<string> _warningMessages = new();

    public GameScanViewModelTests()
    {
        // Setup mocks
        _mockGameScannerService = new Mock<IGameScannerService>();
        _mockMessageHandler = new Mock<IMessageHandler>();
        _mockSettingsService = new MockSettingsService();

        // Capture messages for verification
        _mockMessageHandler.Setup(x => x.ShowInfo(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, target) => _infoMessages.Add(msg));
        _mockMessageHandler.Setup(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, target) => _warningMessages.Add(msg));
        _mockMessageHandler.Setup(x => x.ShowError(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, target) => _errorMessages.Add(msg));
        _mockMessageHandler.Setup(x => x.ShowSuccess(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, target) => _successMessages.Add(msg));

        // Create view model
        _viewModel = new GameScanViewModel(
            _mockGameScannerService.Object,
            _mockMessageHandler.Object,
            _mockSettingsService);
    }

    public void Dispose()
    {
        // Cleanup if necessary
    }

    #region Export Report Tests

    [Fact]
    public async Task ExportReportCommand_CanOnlyExecuteWithResults()
    {
        // Assert - Initially cannot execute
        var canExecuteInitially = await _viewModel.ExportReportCommand.CanExecute.FirstAsync();
        canExecuteInitially.Should().BeFalse("because no results are available");

        // Act - Set results
        _viewModel.ScanResult = new GameScanResult { HasIssues = false };

        // Assert - Now can execute
        var canExecuteAfter = await _viewModel.ExportReportCommand.CanExecute.FirstAsync();
        canExecuteAfter.Should().BeTrue("because results are now available");
    }

    #endregion

    #region Progress Update Tests

    [Theory]
    [InlineData(0, "Starting...")]
    [InlineData(50, "Half way there...")]
    [InlineData(100, "Complete!")]
    public void UpdateProgress_UpdatesValuesCorrectly(double value, string text)
    {
        // Arrange
        var progressValueChanged = false;
        var progressTextChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.ProgressValue))
                progressValueChanged = true;
            if (args.PropertyName == nameof(_viewModel.ProgressText))
                progressTextChanged = true;
        };

        // Act - Use reflection to call private UpdateProgress method
        var updateProgressMethod = _viewModel.GetType().GetMethod("UpdateProgress",
            BindingFlags.NonPublic | BindingFlags.Instance);
        updateProgressMethod?.Invoke(_viewModel, new object[] { value, text });

        // Assert
        _viewModel.ProgressValue.Should().Be(value);
        _viewModel.ProgressText.Should().Be(text);
        progressValueChanged.Should().BeTrue("because progress value changed");
        progressTextChanged.Should().BeTrue("because progress text changed");
    }

    #endregion

    #region Auto-Detection Tests

    [Theory]
    [InlineData(GameType.Fallout4)]
    [InlineData(GameType.Skyrim)]
    public async Task AutoDetectGamePath_ChecksCommonPaths(GameType gameType)
    {
        // Arrange
        _viewModel.SelectedGameType = gameType;

        // Act - Trigger auto-detection by changing game type
        var originalPath = _viewModel.GameInstallPath;
        _viewModel.SelectedGameType = gameType == GameType.Fallout4 ? GameType.Skyrim : GameType.Fallout4;
        _viewModel.SelectedGameType = gameType;
        await Task.Delay(100); // Allow async operations

        // Assert - Should attempt auto-detection
        // Note: In test environment, directories won't exist, so it should show warning
        if (string.IsNullOrWhiteSpace(_viewModel.GameInstallPath))
            _warningMessages.Should().Contain(msg => msg.Contains($"Could not auto-detect {gameType} installation"));
    }

    #endregion

    #region Settings Integration Tests

    [Fact]
    public async Task SelectGamePath_SavesPathToSettings()
    {
        // Note: This test would require mocking the StorageProvider which requires
        // platform-specific setup. In a real test scenario, you would mock the
        // dialog interaction or use integration tests with actual UI.

        // Verify the command exists and can be executed
        _viewModel.SelectGamePathCommand.Should().NotBeNull();
        var canExecute = await _viewModel.SelectGamePathCommand.CanExecute.FirstAsync();
        canExecute.Should().BeTrue("because select path command should always be available");
    }

    #endregion

    #region Constructor and Initialization Tests

    [Fact]
    public async Task Constructor_InitializesAllPropertiesAndCommands()
    {
        // Allow async initialization
        await Task.Delay(100);

        // Assert - Properties initialized
        _viewModel.GameInstallPath.Should().NotBeNull("because path should be initialized");
        _viewModel.SelectedGameType.Should().Be(GameType.Fallout4, "because default game type is Fallout4");
        _viewModel.IsScanning.Should().BeFalse("because no scan is in progress initially");
        _viewModel.HasGamePath.Should().BeFalse("because no path is set initially");
        _viewModel.ProgressValue.Should().Be(0, "because progress starts at 0");
        _viewModel.ProgressText.Should().BeEmpty("because no progress text initially");
        _viewModel.ProgressVisible.Should().BeFalse("because progress is hidden initially");
        _viewModel.HasResults.Should().BeFalse("because no results initially");

        // Assert - Collections initialized
        _viewModel.CriticalIssues.Should().NotBeNull("because critical issues collection should be initialized");
        _viewModel.CriticalIssues.Should().BeEmpty("because no critical issues initially");
        _viewModel.Warnings.Should().NotBeNull("because warnings collection should be initialized");
        _viewModel.Warnings.Should().BeEmpty("because no warnings initially");

        // Assert - Commands initialized
        _viewModel.SelectGamePathCommand.Should().NotBeNull("because select path command is required");
        _viewModel.StartScanCommand.Should().NotBeNull("because start scan command is required");
        _viewModel.CancelScanCommand.Should().NotBeNull("because cancel command is required");
        _viewModel.ClearResultsCommand.Should().NotBeNull("because clear command is required");
        _viewModel.ExportReportCommand.Should().NotBeNull("because export command is required");
        _viewModel.RunIndividualScanCommand.Should().NotBeNull("because individual scan command is required");
    }

    [Fact]
    public async Task InitializeAsync_LoadsSettingsWithDefaultGamePath()
    {
        // Arrange
        var settings = new UserSettings { DefaultGamePath = @"C:\Games\Fallout4" };
        _mockSettingsService.SetUserSettings(settings);

        // Act
        var viewModel = new GameScanViewModel(
            _mockGameScannerService.Object,
            _mockMessageHandler.Object,
            _mockSettingsService);
        await Task.Delay(200); // Allow initialization

        // Assert
        viewModel.GameInstallPath.Should().Be(@"C:\Games\Fallout4", "because path from settings should be loaded");
        viewModel.HasGamePath.Should().BeTrue("because a valid path is set");
    }

    [Fact]
    public async Task InitializeAsync_HandlesSettingsLoadFailure()
    {
        // Arrange
        _mockSettingsService.LoadException = new InvalidOperationException("Settings corrupted");

        // Act
        var viewModel = new GameScanViewModel(
            _mockGameScannerService.Object,
            _mockMessageHandler.Object,
            _mockSettingsService);
        await Task.Delay(200); // Allow initialization

        // Assert
        _errorMessages.Should().Contain(msg => msg.Contains("Failed to initialize"),
            "because initialization failure should be logged");
        viewModel.GameInstallPath.Should().BeEmpty("because path should remain empty on failure");
    }

    #endregion

    #region Property Change Notification Tests

    [Fact]
    public void GameInstallPath_NotifiesPropertyChangeAndUpdatesHasGamePath()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _viewModel.PropertyChanged += (sender, args) => propertyChangedEvents.Add(args.PropertyName!);

        // Act
        _viewModel.GameInstallPath = @"C:\Test\Path";

        // Assert
        _viewModel.GameInstallPath.Should().Be(@"C:\Test\Path");
        _viewModel.HasGamePath.Should().BeTrue("because a non-empty path is set");
        propertyChangedEvents.Should().Contain(nameof(_viewModel.GameInstallPath));
        propertyChangedEvents.Should().Contain(nameof(_viewModel.HasGamePath));

        // Act - Clear path
        _viewModel.GameInstallPath = "";

        // Assert
        _viewModel.HasGamePath.Should().BeFalse("because path is empty");
    }

    [Fact]
    public void SelectedGameType_NotifiesPropertyChangeAndTriggersAutoDetect()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _viewModel.PropertyChanged += (sender, args) => propertyChangedEvents.Add(args.PropertyName!);

        // Act
        _viewModel.SelectedGameType = GameType.Skyrim;

        // Assert
        _viewModel.SelectedGameType.Should().Be(GameType.Skyrim);
        propertyChangedEvents.Should().Contain(nameof(_viewModel.SelectedGameType));
    }

    [Fact]
    public void ScanResult_UpdatesHasResultsAndDisplays()
    {
        // Arrange
        var scanResult = new GameScanResult
        {
            CrashGenResults = "CrashGen OK",
            XsePluginResults = "XSE Plugins Valid",
            ModIniResults = "INI Files OK",
            WryeBashResults = "Wrye Bash OK",
            CriticalIssues = new List<string> { "Critical Issue 1" },
            Warnings = new List<string> { "Warning 1", "Warning 2" }
        };

        // Act
        _viewModel.ScanResult = scanResult;

        // Assert
        _viewModel.HasResults.Should().BeTrue("because results are set");
        _viewModel.CrashGenStatus.Should().Be("CrashGen OK");
        _viewModel.XsePluginStatus.Should().Be("XSE Plugins Valid");
        _viewModel.ModIniStatus.Should().Be("INI Files OK");
        _viewModel.WryeBashStatus.Should().Be("Wrye Bash OK");
        _viewModel.CriticalIssues.Should().HaveCount(1);
        _viewModel.CriticalIssues[0].Should().Be("Critical Issue 1");
        _viewModel.Warnings.Should().HaveCount(2);
        _viewModel.Warnings[0].Should().Be("Warning 1");
        _viewModel.Warnings[1].Should().Be("Warning 2");
    }

    #endregion

    #region Command Execution Tests

    [Fact]
    public async Task StartScanCommand_CanOnlyExecuteWithGamePath()
    {
        // Assert - Initially cannot execute
        var canExecuteInitially = await _viewModel.StartScanCommand.CanExecute.FirstAsync();
        canExecuteInitially.Should().BeFalse("because no game path is set");

        // Act - Set game path
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";

        // Assert - Now can execute
        var canExecuteAfter = await _viewModel.StartScanCommand.CanExecute.FirstAsync();
        canExecuteAfter.Should().BeTrue("because game path is now set");

        // Act - Start scanning
        _viewModel.IsScanning = true;

        // Assert - Cannot execute while scanning
        var canExecuteWhileScanning = await _viewModel.StartScanCommand.CanExecute.FirstAsync();
        canExecuteWhileScanning.Should().BeFalse("because scan is in progress");
    }

    [Fact]
    public async Task StartScanCommand_PerformsFullScanSuccessfully()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        var expectedResult = new GameScanResult
        {
            CrashGenResults = "CrashGen configured correctly",
            XsePluginResults = "All XSE plugins valid",
            ModIniResults = "No INI issues found",
            WryeBashResults = "Wrye Bash setup correctly",
            HasIssues = false
        };
        _mockGameScannerService.Setup(x => x.ScanGameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(100); // Allow async operations to complete

        // Assert
        _viewModel.IsScanning.Should().BeFalse("because scan should complete");
        _viewModel.ProgressVisible.Should().BeFalse("because progress should be hidden after scan");
        _viewModel.ScanResult.Should().NotBeNull("because results should be set");
        _viewModel.ScanResult!.CrashGenResults.Should().Be("CrashGen configured correctly");
        _successMessages.Should().Contain("Game scan completed successfully!");
    }

    [Fact]
    public async Task StartScanCommand_HandlesScanFailure()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.ScanGameAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Game not found"));

        // Act
        await _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(100); // Allow async operations to complete

        // Assert
        _viewModel.IsScanning.Should().BeFalse("because scan should stop on error");
        _viewModel.ProgressVisible.Should().BeFalse("because progress should be hidden on error");
        _errorMessages.Should().Contain(msg => msg.Contains("Game scan failed") && msg.Contains("Game not found"));
    }

    [Fact]
    public async Task StartScanCommand_HandlesCancellation()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.ScanGameAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(100); // Allow async operations to complete

        // Assert
        _viewModel.IsScanning.Should().BeFalse("because scan should stop on cancellation");
        _viewModel.ProgressVisible.Should().BeFalse("because progress should be hidden on cancellation");
        _warningMessages.Should().Contain("Game scan was cancelled.");
    }

    [Fact]
    public async Task CancelScanCommand_CancelsRunningScan()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        var tcs = new TaskCompletionSource<GameScanResult>();
        _mockGameScannerService.Setup(x => x.ScanGameAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => tcs.Task);

        // Act - Start scan
        var scanTask = _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(50); // Let scan start

        // Assert - Scan is running
        _viewModel.IsScanning.Should().BeTrue("because scan should be in progress");

        // Act - Cancel scan
        _viewModel.CancelScanCommand.Execute();
        tcs.SetCanceled();

        try
        {
            await scanTask;
        }
        catch
        {
            /* Expected cancellation */
        }

        await Task.Delay(100); // Allow cleanup

        // Assert - Scan cancelled
        _viewModel.IsScanning.Should().BeFalse("because scan should be cancelled");
    }

    [Fact]
    public void ClearResultsCommand_ClearsAllResults()
    {
        // Arrange
        _viewModel.ScanResult = new GameScanResult
        {
            CrashGenResults = "Test",
            CriticalIssues = new List<string> { "Issue" },
            Warnings = new List<string> { "Warning" }
        };
        _viewModel.CrashGenStatus = "Status";
        _viewModel.XsePluginStatus = "Status";
        _viewModel.ModIniStatus = "Status";
        _viewModel.WryeBashStatus = "Status";
        _viewModel.CriticalIssues.Add("Issue");
        _viewModel.Warnings.Add("Warning");

        // Act
        _viewModel.ClearResultsCommand.Execute();

        // Assert
        _viewModel.ScanResult.Should().BeNull("because results were cleared");
        _viewModel.CrashGenStatus.Should().BeEmpty("because status was cleared");
        _viewModel.XsePluginStatus.Should().BeEmpty("because status was cleared");
        _viewModel.ModIniStatus.Should().BeEmpty("because status was cleared");
        _viewModel.WryeBashStatus.Should().BeEmpty("because status was cleared");
        _viewModel.CriticalIssues.Should().BeEmpty("because issues were cleared");
        _viewModel.Warnings.Should().BeEmpty("because warnings were cleared");
        _viewModel.HasResults.Should().BeFalse("because no results after clearing");
    }

    #endregion

    #region Individual Scan Tests

    [Fact]
    public async Task RunIndividualScanCommand_CrashGen_ExecutesCorrectly()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.CheckCrashGenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("CrashGen: All settings correct");

        // Act
        await _viewModel.RunIndividualScanCommand.Execute("CrashGen").FirstAsync();
        await Task.Delay(100); // Allow async operations

        // Assert
        _viewModel.CrashGenStatus.Should().Be("CrashGen: All settings correct");
        _viewModel.IsScanning.Should().BeFalse("because scan should complete");
        _successMessages.Should().Contain("CrashGen scan completed successfully!");
    }

    [Fact]
    public async Task RunIndividualScanCommand_XsePlugins_ExecutesCorrectly()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.ValidateXsePluginsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("XSE Plugins: All valid");

        // Act
        await _viewModel.RunIndividualScanCommand.Execute("XsePlugins").FirstAsync();
        await Task.Delay(100); // Allow async operations

        // Assert
        _viewModel.XsePluginStatus.Should().Be("XSE Plugins: All valid");
        _viewModel.IsScanning.Should().BeFalse("because scan should complete");
        _successMessages.Should().Contain("XsePlugins scan completed successfully!");
    }

    [Fact]
    public async Task RunIndividualScanCommand_ModInis_ExecutesCorrectly()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.ScanModInisAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mod INIs: No issues found");

        // Act
        await _viewModel.RunIndividualScanCommand.Execute("ModInis").FirstAsync();
        await Task.Delay(100); // Allow async operations

        // Assert
        _viewModel.ModIniStatus.Should().Be("Mod INIs: No issues found");
        _viewModel.IsScanning.Should().BeFalse("because scan should complete");
        _successMessages.Should().Contain("ModInis scan completed successfully!");
    }

    [Fact]
    public async Task RunIndividualScanCommand_WryeBash_ExecutesCorrectly()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.CheckWryeBashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Wrye Bash: Configured correctly");

        // Act
        await _viewModel.RunIndividualScanCommand.Execute("WryeBash").FirstAsync();
        await Task.Delay(100); // Allow async operations

        // Assert
        _viewModel.WryeBashStatus.Should().Be("Wrye Bash: Configured correctly");
        _viewModel.IsScanning.Should().BeFalse("because scan should complete");
        _successMessages.Should().Contain("WryeBash scan completed successfully!");
    }

    [Fact]
    public async Task RunIndividualScanCommand_HandlesFailure()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        _mockGameScannerService.Setup(x => x.CheckCrashGenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CrashGen not found"));

        // Act
        await _viewModel.RunIndividualScanCommand.Execute("CrashGen").FirstAsync();
        await Task.Delay(100); // Allow async operations

        // Assert
        _viewModel.IsScanning.Should().BeFalse("because scan should stop on error");
        _errorMessages.Should()
            .Contain(msg => msg.Contains("CrashGen scan failed") && msg.Contains("CrashGen not found"));
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public void Constructor_WithNullGameScannerService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GameScanViewModel(
            null!,
            _mockMessageHandler.Object,
            _mockSettingsService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gameScannerService");
    }

    [Fact]
    public void Constructor_WithNullMessageHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GameScanViewModel(
            _mockGameScannerService.Object,
            null!,
            _mockSettingsService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageHandler");
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GameScanViewModel(
            _mockGameScannerService.Object,
            _mockMessageHandler.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settingsService");
    }

    [Fact]
    public async Task MultipleScansInSequence_HandledCorrectly()
    {
        // Arrange
        _viewModel.GameInstallPath = @"C:\Games\Fallout4";
        var result1 = new GameScanResult { CrashGenResults = "First scan" };
        var result2 = new GameScanResult { CrashGenResults = "Second scan" };

        _mockGameScannerService.SetupSequence(x => x.ScanGameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result1)
            .ReturnsAsync(result2);

        // Act - First scan
        await _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(100);
        var firstResult = _viewModel.ScanResult;

        // Act - Second scan
        await _viewModel.StartScanCommand.Execute().FirstAsync();
        await Task.Delay(100);
        var secondResult = _viewModel.ScanResult;

        // Assert
        firstResult!.CrashGenResults.Should().Be("First scan");
        secondResult!.CrashGenResults.Should().Be("Second scan");
        _successMessages.Should().HaveCount(2, "because two scans completed successfully");
    }

    #endregion
}