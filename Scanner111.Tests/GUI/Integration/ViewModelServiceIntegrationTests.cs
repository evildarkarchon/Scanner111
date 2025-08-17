using System.Reactive.Linq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.Tests.GUI.Integration;

/// <summary>
///     Integration tests that verify the interaction between ViewModels and Services.
///     These tests ensure that ViewModels correctly utilize services and that the
///     entire GUI layer works together properly.
/// </summary>
[Collection("GUI Tests")]
public class ViewModelServiceIntegrationTests : IDisposable
{
    private readonly GuiMessageHandlerService _messageHandler;
    private readonly Mock<ICacheManager> _mockCacheManager;
    private readonly Mock<IUnsolvedLogsMover> _mockUnsolvedLogsMover;
    private readonly Mock<IUpdateService> _mockUpdateService;
    private readonly Mock<IServiceProvider> _serviceProvider;
    private readonly SettingsService _settingsService;
    private readonly string _testDirectory;

    public ViewModelServiceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Use real services where possible
        _settingsService = new SettingsService();
        _messageHandler = new GuiMessageHandlerService();

        // Mock services that have external dependencies
        _mockUpdateService = new Mock<IUpdateService>();
        _mockCacheManager = new Mock<ICacheManager>();
        _mockUnsolvedLogsMover = new Mock<IUnsolvedLogsMover>();
        _serviceProvider = new Mock<IServiceProvider>();

        // Set test environment
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH",
            Path.Combine(_testDirectory, "settings.json"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", null);
        if (Directory.Exists(_testDirectory))
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
    }

    [Fact]
    public async Task MainWindowViewModel_MessageHandler_Integration()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _serviceProvider.Object,
            _settingsService,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        // Wait for async initialization
        await Task.Delay(100);

        // Act - Simulate operations that should generate messages
        viewModel.AddLogMessage("Test message");
        _messageHandler.ShowInfo("Status update");
        _messageHandler.ShowWarning("Warning message");
        _messageHandler.ShowError("Error occurred");

        // Assert
        viewModel.LogMessages.Should().Contain(m => m.Contains("Test message"),
            "because log message was added directly");
        viewModel.StatusText.Should().Be("Error: Error occurred",
            "because the last message was an error");
    }

    [Fact]
    public async Task MainWindowViewModel_Settings_Integration()
    {
        // Arrange - Save some settings first
        var settings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Integration\test.log",
            DefaultGamePath = @"C:\Games\Integration",
            AutoSaveResults = true,
            EnableUpdateCheck = false
        };
        await _settingsService.SaveSettingsAsync(settings);

        // Act - Create view model which should load settings
        var viewModel = new MainWindowViewModel(
            _serviceProvider.Object,
            _settingsService,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        // Wait for async initialization
        await Task.Delay(200);

        // Assert
        _mockUpdateService.Verify(u => u.IsLatestVersionAsync(It.IsAny<bool>(), default),
            Times.Never, "because update check is disabled in settings");
    }

    [Fact]
    public async Task SettingsWindowViewModel_SaveLoad_Integration()
    {
        // Arrange
        var settingsViewModel = new SettingsWindowViewModel(_settingsService);
        await Task.Delay(100); // Wait for initialization

        // Modify settings
        settingsViewModel.DefaultLogPath = @"C:\Integration\modified.log";
        settingsViewModel.MaxLogMessages = 250;
        settingsViewModel.EnableDebugLogging = true;
        settingsViewModel.FcxMode = true;

        // Act - Save settings
        var windowClosed = false;
        settingsViewModel.CloseWindow = () => windowClosed = true;
        await settingsViewModel.SaveCommand.Execute().FirstAsync();

        // Assert - Settings were saved
        windowClosed.Should().BeTrue("because window closes after successful save");

        // Verify persistence by creating new view model
        var newSettingsViewModel = new SettingsWindowViewModel(_settingsService);
        await Task.Delay(100); // Wait for initialization

        newSettingsViewModel.DefaultLogPath.Should().Be(@"C:\Integration\modified.log",
            "because settings were persisted");
        newSettingsViewModel.MaxLogMessages.Should().Be(250,
            "because max messages setting was persisted");
        newSettingsViewModel.EnableDebugLogging.Should().BeTrue(
            "because debug logging setting was persisted");
        newSettingsViewModel.FcxMode.Should().BeTrue(
            "because FCX mode setting was persisted");
    }

    [Fact]
    public async Task MainWindowViewModel_Progress_Integration()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _serviceProvider.Object,
            _settingsService,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        await Task.Delay(100); // Wait for initialization

        // Act - Use message handler to show progress
        using (var progressContext = _messageHandler.CreateProgressContext("Integration Test", 100))
        {
            progressContext.Update(25, "Processing step 1");

            // Assert - During progress
            viewModel.ProgressVisible.Should().BeTrue("because progress is active");
            viewModel.ProgressText.Should().Be("Integration Test: Processing step 1",
                "because progress text is updated");
            viewModel.ProgressValue.Should().Be(25.0, "because progress is at 25%");

            progressContext.Update(75, "Processing step 2");
            viewModel.ProgressValue.Should().Be(75.0, "because progress is at 75%");

            progressContext.Complete();
            viewModel.ProgressValue.Should().Be(100.0, "because progress is complete");
        }

        // Assert - After disposal
        viewModel.ProgressVisible.Should().BeFalse("because progress context was disposed");
    }

    [Fact]
    public async Task ViewModels_SharedSettings_Consistency()
    {
        // Arrange - Create and save initial settings
        var initialSettings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Shared\test.log",
            DefaultGamePath = @"C:\Games\Shared",
            MaxLogMessages = 150,
            EnableUpdateCheck = true
        };
        await _settingsService.SaveSettingsAsync(initialSettings);

        // Act - Create multiple view models that use the same settings
        var mainViewModel = new MainWindowViewModel(
            _settingsService,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        var settingsViewModel = new SettingsWindowViewModel(_settingsService);

        await Task.Delay(200); // Wait for initialization

        // Assert - Both view models should have consistent settings
        settingsViewModel.DefaultLogPath.Should().Be(@"C:\Shared\test.log",
            "because settings view model loaded the saved settings");
        settingsViewModel.DefaultGamePath.Should().Be(@"C:\Games\Shared",
            "because settings view model loaded the saved settings");
        settingsViewModel.MaxLogMessages.Should().Be(150,
            "because settings view model loaded the saved settings");

        // Main view model should have attempted update check
        _mockUpdateService.Verify(u => u.IsLatestVersionAsync(It.IsAny<bool>(), default),
            Times.Once, "because update check is enabled in settings");
    }

    [Fact]
    public async Task SettingsWindowViewModel_RecentFiles_Integration()
    {
        // Arrange
        var userSettings = new UserSettings
        {
            MaxRecentItems = 5
        };
        userSettings.RecentLogFiles.Add(@"C:\recent1.log");
        userSettings.RecentLogFiles.Add(@"C:\recent2.log");
        userSettings.RecentGamePaths.Add(@"C:\Games\Recent");
        userSettings.RecentScanDirectories.Add(@"C:\Scans\Recent");

        await _settingsService.SaveUserSettingsAsync(userSettings);

        // Act
        var settingsViewModel = new SettingsWindowViewModel(_settingsService);
        await Task.Delay(100); // Wait for initialization

        // Assert
        settingsViewModel.RecentLogFiles.Should().HaveCount(2,
            "because two recent log files were saved");
        settingsViewModel.RecentLogFiles.Should().Contain(@"C:\recent1.log",
            "because it was in saved settings");
        settingsViewModel.RecentGamePaths.Should().HaveCount(1,
            "because one recent game path was saved");
        settingsViewModel.RecentScanDirectories.Should().HaveCount(1,
            "because one recent scan directory was saved");

        settingsViewModel.HasRecentLogFiles.Should().BeTrue(
            "because recent log files exist");
        settingsViewModel.HasRecentGamePaths.Should().BeTrue(
            "because recent game paths exist");
        settingsViewModel.HasRecentScanDirectories.Should().BeTrue(
            "because recent scan directories exist");
    }

    [Fact]
    public async Task SettingsWindowViewModel_ClearRecentFiles_Integration()
    {
        // Arrange
        var userSettings = new UserSettings();
        userSettings.RecentLogFiles.Add(@"C:\tobecleared.log");
        userSettings.RecentGamePaths.Add(@"C:\Games\ToBeCleared");
        await _settingsService.SaveUserSettingsAsync(userSettings);

        var settingsViewModel = new SettingsWindowViewModel(_settingsService);
        await Task.Delay(100); // Wait for initialization

        // Act
        await settingsViewModel.ClearRecentFilesCommand.Execute().FirstAsync();

        // Assert
        settingsViewModel.RecentLogFiles.Should().BeEmpty(
            "because recent files were cleared");
        settingsViewModel.RecentGamePaths.Should().BeEmpty(
            "because recent paths were cleared");
        settingsViewModel.HasRecentLogFiles.Should().BeFalse(
            "because no recent files exist after clearing");
    }

    [Fact]
    public async Task MainWindowViewModel_ScanWithSettings_Integration()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoSaveResults = true,
            DefaultOutputFormat = "markdown"
        };
        await _settingsService.SaveSettingsAsync(settings);

        var viewModel = new MainWindowViewModel(
            _serviceProvider.Object,
            _settingsService,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        await Task.Delay(100); // Wait for initialization

        // Create a test log file
        var testLog = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testLog, "Test crash log content");
        viewModel.SelectedLogPath = testLog;

        // Act
        await viewModel.ScanCommand.Execute().FirstAsync();

        // Assert
        viewModel.ScanResults.Should().HaveCount(1,
            "because one file was scanned");
        viewModel.LogMessages.Should().Contain(m => m.Contains("completed") || m.Contains("Scan completed"),
            "because scan completion should be logged");
    }

    [Fact]
    public async Task ViewModels_ErrorHandling_Integration()
    {
        // Arrange - Create settings service that will fail
        var failingSettingsService = new Mock<ISettingsService>();
        failingSettingsService
            .Setup(s => s.LoadSettingsAsync())
            .ThrowsAsync(new IOException("Settings file locked"));
        failingSettingsService
            .Setup(s => s.LoadUserSettingsAsync())
            .ThrowsAsync(new IOException("Settings file locked"));
        failingSettingsService
            .Setup(s => s.GetDefaultSettings())
            .Returns(new ApplicationSettings());

        // Act & Assert - ViewModels should handle the error gracefully
        var mainViewModel = new MainWindowViewModel(
            failingSettingsService.Object,
            _messageHandler,
            _mockUpdateService.Object,
            _mockCacheManager.Object,
            _mockUnsolvedLogsMover.Object);

        await Task.Delay(100); // Wait for initialization

        // Should not throw and should have some default state
        mainViewModel.Should().NotBeNull("because view model should be created despite error");
        mainViewModel.StatusText.Should().NotBeNullOrEmpty("because status should be set");
    }

    [Fact]
    public async Task SettingsViewModel_Cancel_RestoresOriginalValues()
    {
        // Arrange
        var originalSettings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Original\path.log",
            MaxLogMessages = 100
        };
        await _settingsService.SaveSettingsAsync(originalSettings);

        var settingsViewModel = new SettingsWindowViewModel(_settingsService);
        await Task.Delay(100); // Wait for initialization

        // Modify settings
        settingsViewModel.DefaultLogPath = @"C:\Modified\path.log";
        settingsViewModel.MaxLogMessages = 200;

        // Act - Cancel changes
        var windowClosed = false;
        settingsViewModel.CloseWindow = () => windowClosed = true;
        await settingsViewModel.CancelCommand.Execute().FirstAsync();

        // Assert
        windowClosed.Should().BeTrue("because window closes on cancel");
        settingsViewModel.DefaultLogPath.Should().Be(@"C:\Original\path.log",
            "because original value was restored");
        settingsViewModel.MaxLogMessages.Should().Be(100,
            "because original value was restored");

        // Verify settings were not saved
        var loadedSettings = await _settingsService.LoadSettingsAsync();
        loadedSettings.DefaultLogPath.Should().Be(@"C:\Original\path.log",
            "because cancelled changes were not persisted");
    }
}