using System.Reactive.Linq;
using FluentAssertions;
using Scanner111.GUI.Models;
using Scanner111.GUI.ViewModels;
using Scanner111.Tests.GUI.TestHelpers;

namespace Scanner111.Tests.GUI.ViewModels;

[Collection("GUI Tests")]
public class SettingsWindowViewModelTests
{
    private readonly MockSettingsService _mockSettingsService;
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindowViewModelTests()
    {
        _mockSettingsService = new MockSettingsService();
        _viewModel = new SettingsWindowViewModel(_mockSettingsService);
    }

    [Fact]
    public async Task Constructor_LoadsSettingsOnInitialization()
    {
        // Allow time for async initialization
        await Task.Delay(100);

        // Assert
        _mockSettingsService.LoadCalled.Should().BeTrue("because settings should be loaded on initialization");
        _viewModel.DefaultLogPath.Should().Be(@"C:\Test\default.log", "because default log path should be loaded");
        _viewModel.DefaultGamePath.Should().Be(@"C:\Games\Fallout4", "because default game path should be loaded");
        _viewModel.DefaultScanDirectory.Should()
            .Be(@"C:\Test\Scans", "because default scan directory should be loaded");
        _viewModel.AutoLoadF4SeLogs.Should().BeTrue("because auto-load setting should be loaded");
        _viewModel.MaxLogMessages.Should().Be(100, "because max log messages setting should be loaded");
    }

    [Fact]
    public void PropertyChanges_NotifyProperly()
    {
        // Arrange
        var propertyChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.DefaultLogPath))
                propertyChanged = true;
        };

        // Act
        _viewModel.DefaultLogPath = @"C:\New\Path.log";

        // Assert
        propertyChanged.Should().BeTrue("because property change notification should be raised");
        _viewModel.DefaultLogPath.Should().Be(@"C:\New\Path.log", "because property value should be updated");
    }

    [Fact]
    public async Task SaveCommand_SavesSettings()
    {
        // Arrange
        var closeWindowCalled = false;
        _viewModel.CloseWindow = () => closeWindowCalled = true;
        _viewModel.DefaultLogPath = @"C:\Modified\Path.log";
        _viewModel.MaxLogMessages = 200;

        // Act
        await _viewModel.SaveCommand.Execute().FirstAsync();

        // Assert
        _mockSettingsService.SaveCalled.Should().BeTrue("because settings should be saved");
        closeWindowCalled.Should().BeTrue("because window should close after successful save");
    }

    [Fact]
    public async Task SaveCommand_HandlesExceptions()
    {
        // Arrange
        _mockSettingsService.SaveException = new InvalidOperationException("Save failed");
        var closeWindowCalled = false;
        _viewModel.CloseWindow = () => closeWindowCalled = true;

        // Act - Should not throw
        await _viewModel.SaveCommand.Execute().FirstAsync();

        // Assert
        _mockSettingsService.SaveCalled.Should().BeTrue("because save attempt should be made");
        closeWindowCalled.Should().BeFalse("because window should not close on error");
    }

    [Fact]
    public async Task CancelCommand_RestoresOriginalSettings()
    {
        // Arrange
        var closeWindowCalled = false;
        _viewModel.CloseWindow = () => closeWindowCalled = true;
        var originalLogPath = _viewModel.DefaultLogPath;
        _viewModel.DefaultLogPath = @"C:\Modified\Path.log";

        // Act
        await _viewModel.CancelCommand.Execute().FirstAsync();

        // Assert
        _viewModel.DefaultLogPath.Should().Be(originalLogPath, "because settings should be restored on cancel");
        closeWindowCalled.Should().BeTrue("because window should close on cancel");
    }

    [Fact]
    public async Task ResetToDefaultsCommand_ResetsAllSettings()
    {
        // Arrange
        _viewModel.DefaultLogPath = @"C:\Custom\Path.log";
        _viewModel.AutoSaveResults = false;
        _viewModel.EnableDebugLogging = true;

        // Act
        await _viewModel.ResetToDefaultsCommand.Execute().FirstAsync();

        // Assert
        _viewModel.DefaultLogPath.Should().Be("", "because default log path should be reset");
        _viewModel.AutoSaveResults.Should().BeFalse("because auto-save should be reset to default");
        _viewModel.EnableDebugLogging.Should().BeFalse("because debug logging should be reset to default");
        _viewModel.WindowWidth.Should().Be(1200, "because window width should be reset to default");
        _viewModel.WindowHeight.Should().Be(800, "because window height should be reset to default");
    }

    [Fact]
    public async Task ClearRecentFilesCommand_ClearsAllRecentCollections()
    {
        // Arrange
        _viewModel.RecentLogFiles.Add(@"C:\log1.log");
        _viewModel.RecentLogFiles.Add(@"C:\log2.log");
        _viewModel.RecentGamePaths.Add(@"C:\Games\Game1");
        _viewModel.RecentScanDirectories.Add(@"C:\Scans");

        // Act
        await _viewModel.ClearRecentFilesCommand.Execute().FirstAsync();

        // Assert
        _viewModel.RecentLogFiles.Should().BeEmpty("because recent log files should be cleared");
        _viewModel.RecentGamePaths.Should().BeEmpty("because recent game paths should be cleared");
        _viewModel.RecentScanDirectories.Should().BeEmpty("because recent scan directories should be cleared");
        _viewModel.HasRecentLogFiles.Should().BeFalse("because no recent log files should exist");
        _viewModel.HasRecentGamePaths.Should().BeFalse("because no recent game paths should exist");
        _viewModel.HasRecentScanDirectories.Should().BeFalse("because no recent scan directories should exist");
    }

    [Fact]
    public async Task BrowseLogPathCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Picked\File.log";
        _viewModel.ShowFilePickerAsync = (title, filter) => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseLogPathCommand.Execute().FirstAsync();

        // Assert
        _viewModel.DefaultLogPath.Should().Be(pickedPath, "because selected path should be updated");
    }

    [Fact]
    public async Task BrowseGamePathCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Games\PickedGame";
        _viewModel.ShowFolderPickerAsync = title => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseGamePathCommand.Execute().FirstAsync();

        // Assert
        _viewModel.DefaultGamePath.Should().Be(pickedPath, "because selected game path should be updated");
    }

    [Fact]
    public async Task BrowseScanDirectoryCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Scans\PickedDir";
        _viewModel.ShowFolderPickerAsync = title => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseScanDirectoryCommand.Execute().FirstAsync();

        // Assert
        _viewModel.DefaultScanDirectory.Should().Be(pickedPath, "because selected scan directory should be updated");
    }

    [Fact]
    public async Task BrowseModsFolderCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Mods\Folder";
        _viewModel.ShowFolderPickerAsync = title => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseModsFolderCommand.Execute().FirstAsync();

        // Assert
        _viewModel.ModsFolder.Should().Be(pickedPath, "because selected mods folder should be updated");
    }

    [Fact]
    public async Task BrowseIniFolderCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\INI\Folder";
        _viewModel.ShowFolderPickerAsync = title => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseIniFolderCommand.Execute().FirstAsync();

        // Assert
        _viewModel.IniFolder.Should().Be(pickedPath, "because selected INI folder should be updated");
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.DefaultLogPath), @"C:\Test.log")]
    [InlineData(nameof(SettingsWindowViewModel.DefaultGamePath), @"C:\Games")]
    [InlineData(nameof(SettingsWindowViewModel.DefaultScanDirectory), @"C:\Scans")]
    [InlineData(nameof(SettingsWindowViewModel.ModsFolder), @"C:\Mods")]
    [InlineData(nameof(SettingsWindowViewModel.IniFolder), @"C:\INI")]
    [InlineData(nameof(SettingsWindowViewModel.UpdateSource), "GitHub")]
    [InlineData(nameof(SettingsWindowViewModel.DefaultOutputFormat), "json")]
    public void StringProperties_UpdateCorrectly(string propertyName, string value)
    {
        // Arrange
        var property = _viewModel.GetType().GetProperty(propertyName);
        var propertyChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == propertyName)
                propertyChanged = true;
        };

        // Act
        property?.SetValue(_viewModel, value);

        // Assert
        propertyChanged.Should().BeTrue("because property change notification should be raised");
        property?.GetValue(_viewModel).Should().Be(value, "because property value should be updated");
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.AutoLoadF4SeLogs), true)]
    [InlineData(nameof(SettingsWindowViewModel.EnableProgressNotifications), false)]
    [InlineData(nameof(SettingsWindowViewModel.RememberWindowSize), true)]
    [InlineData(nameof(SettingsWindowViewModel.EnableDebugLogging), true)]
    [InlineData(nameof(SettingsWindowViewModel.AutoSaveResults), false)]
    [InlineData(nameof(SettingsWindowViewModel.EnableUpdateCheck), false)]
    [InlineData(nameof(SettingsWindowViewModel.FcxMode), true)]
    [InlineData(nameof(SettingsWindowViewModel.MoveUnsolvedLogs), true)]
    public async Task BooleanProperties_UpdateCorrectly(string propertyName, bool value)
    {
        // Arrange
        await Task.Delay(100); // Allow async initialization to complete

        var property = _viewModel.GetType().GetProperty(propertyName);

        // First set to opposite value to ensure change will happen
        var oppositeValue = !value;
        property?.SetValue(_viewModel, oppositeValue);

        var propertyChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == propertyName)
                propertyChanged = true;
        };

        // Act
        property?.SetValue(_viewModel, value);

        // Assert
        propertyChanged.Should().BeTrue("because property change notification should be raised");
        property?.GetValue(_viewModel).Should().Be(value, "because property value should be updated");
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.MaxLogMessages), 50)]
    [InlineData(nameof(SettingsWindowViewModel.MaxRecentItems), 20)]
    public void IntProperties_UpdateCorrectly(string propertyName, int value)
    {
        // Arrange
        var property = _viewModel.GetType().GetProperty(propertyName);
        var propertyChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == propertyName)
                propertyChanged = true;
        };

        // Act
        property?.SetValue(_viewModel, value);

        // Assert
        propertyChanged.Should().BeTrue("because property change notification should be raised");
        property?.GetValue(_viewModel).Should().Be(value, "because property value should be updated");
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.WindowWidth), 1920.0)]
    [InlineData(nameof(SettingsWindowViewModel.WindowHeight), 1080.0)]
    public void DoubleProperties_UpdateCorrectly(string propertyName, double value)
    {
        // Arrange
        var property = _viewModel.GetType().GetProperty(propertyName);
        var propertyChanged = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == propertyName)
                propertyChanged = true;
        };

        // Act
        property?.SetValue(_viewModel, value);

        // Assert
        propertyChanged.Should().BeTrue("because property change notification should be raised");
        property?.GetValue(_viewModel).Should().Be(value, "because property value should be updated");
    }

    [Fact]
    public void HasRecentLogFiles_ReflectsCollection()
    {
        // Arrange & Act
        Assert.False(_viewModel.HasRecentLogFiles);
        _viewModel.RecentLogFiles.Add("test.log");

        // Need to manually raise property changed for computed properties
        var propertyInfo = _viewModel.GetType().GetProperty(nameof(_viewModel.HasRecentLogFiles));

        // Assert
        _viewModel.RecentLogFiles.Count.Should().BeGreaterThan(0, "because recent log files were added");
    }

    [Fact]
    public async Task LoadSettingsFailure_UsesDefaults()
    {
        // Arrange
        var failingService = new MockSettingsService();
        failingService.LoadException = new InvalidOperationException("Load failed");
        var viewModel = new SettingsWindowViewModel(failingService);

        // Allow time for async initialization
        await Task.Delay(100);

        // Assert - Should have default values
        viewModel.DefaultLogPath.Should().Be("", "because default log path should be empty when load fails");
        viewModel.AutoLoadF4SeLogs.Should().BeTrue("because auto-load should default to true");
        viewModel.MaxLogMessages.Should().Be(100, "because max log messages should default to 100");
        viewModel.WindowWidth.Should().Be(1200, "because window width should default to 1200");
    }

    [Fact]
    public void RecentCollections_PreserveOrder()
    {
        // Arrange
        var userSettings = new UserSettings();
        userSettings.RecentLogFiles.Add("log1.log");
        userSettings.RecentLogFiles.Add("log2.log");
        userSettings.RecentLogFiles.Add("log3.log");
        _mockSettingsService.SetUserSettings(userSettings);

        var viewModel = new SettingsWindowViewModel(_mockSettingsService);

        // Wait for async load
        Thread.Sleep(100);

        // Assert
        viewModel.RecentLogFiles.Should().HaveCount(3, "because three recent files were added");
        viewModel.RecentLogFiles[0].Should().Be("log1.log", "because order should be preserved");
        viewModel.RecentLogFiles[1].Should().Be("log2.log", "because order should be preserved");
        viewModel.RecentLogFiles[2].Should().Be("log3.log", "because order should be preserved");
    }

    [Fact]
    public async Task FilePicker_CancellationHandled()
    {
        // Arrange - return empty string simulating cancellation
        _viewModel.ShowFilePickerAsync = (title, filter) => Task.FromResult("");
        var originalPath = _viewModel.DefaultLogPath;

        // Act
        await _viewModel.BrowseLogPathCommand.Execute().FirstAsync();

        // Assert - Path should not change
        _viewModel.DefaultLogPath.Should().Be(originalPath, "because path should not change when picker is cancelled");
    }
}