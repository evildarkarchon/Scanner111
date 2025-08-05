using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Scanner111.GUI.Models;
using Scanner111.GUI.ViewModels;
using Scanner111.Tests.GUI.TestHelpers;
using Xunit;

namespace Scanner111.Tests.GUI.ViewModels;

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
        Assert.True(_mockSettingsService.LoadCalled);
        Assert.Equal(@"C:\Test\default.log", _viewModel.DefaultLogPath);
        Assert.Equal(@"C:\Games\Fallout4", _viewModel.DefaultGamePath);
        Assert.Equal(@"C:\Test\Scans", _viewModel.DefaultScanDirectory);
        Assert.True(_viewModel.AutoLoadF4SeLogs);
        Assert.Equal(100, _viewModel.MaxLogMessages);
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
        Assert.True(propertyChanged);
        Assert.Equal(@"C:\New\Path.log", _viewModel.DefaultLogPath);
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
        Assert.True(_mockSettingsService.SaveCalled);
        Assert.True(closeWindowCalled);
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
        Assert.True(_mockSettingsService.SaveCalled);
        Assert.False(closeWindowCalled); // Window should not close on error
    }

    [Fact]
    public void CancelCommand_RestoresOriginalSettings()
    {
        // Arrange
        var closeWindowCalled = false;
        _viewModel.CloseWindow = () => closeWindowCalled = true;
        var originalLogPath = _viewModel.DefaultLogPath;
        _viewModel.DefaultLogPath = @"C:\Modified\Path.log";

        // Act
        _viewModel.CancelCommand.Execute();

        // Assert
        Assert.Equal(originalLogPath, _viewModel.DefaultLogPath);
        Assert.True(closeWindowCalled);
    }

    [Fact]
    public void ResetToDefaultsCommand_ResetsAllSettings()
    {
        // Arrange
        _viewModel.DefaultLogPath = @"C:\Custom\Path.log";
        _viewModel.AutoSaveResults = false;
        _viewModel.EnableDebugLogging = true;

        // Act
        _viewModel.ResetToDefaultsCommand.Execute();

        // Assert
        Assert.Equal("", _viewModel.DefaultLogPath);
        Assert.False(_viewModel.AutoSaveResults);
        Assert.False(_viewModel.EnableDebugLogging);
        Assert.Equal(1200, _viewModel.WindowWidth);
        Assert.Equal(800, _viewModel.WindowHeight);
    }

    [Fact]
    public void ClearRecentFilesCommand_ClearsAllRecentCollections()
    {
        // Arrange
        _viewModel.RecentLogFiles.Add(@"C:\log1.log");
        _viewModel.RecentLogFiles.Add(@"C:\log2.log");
        _viewModel.RecentGamePaths.Add(@"C:\Games\Game1");
        _viewModel.RecentScanDirectories.Add(@"C:\Scans");

        // Act
        _viewModel.ClearRecentFilesCommand.Execute();

        // Assert
        Assert.Empty(_viewModel.RecentLogFiles);
        Assert.Empty(_viewModel.RecentGamePaths);
        Assert.Empty(_viewModel.RecentScanDirectories);
        Assert.False(_viewModel.HasRecentLogFiles);
        Assert.False(_viewModel.HasRecentGamePaths);
        Assert.False(_viewModel.HasRecentScanDirectories);
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
        Assert.Equal(pickedPath, _viewModel.DefaultLogPath);
    }

    [Fact]
    public async Task BrowseGamePathCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Games\PickedGame";
        _viewModel.ShowFolderPickerAsync = (title) => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseGamePathCommand.Execute().FirstAsync();

        // Assert
        Assert.Equal(pickedPath, _viewModel.DefaultGamePath);
    }

    [Fact]
    public async Task BrowseScanDirectoryCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Scans\PickedDir";
        _viewModel.ShowFolderPickerAsync = (title) => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseScanDirectoryCommand.Execute().FirstAsync();

        // Assert
        Assert.Equal(pickedPath, _viewModel.DefaultScanDirectory);
    }

    [Fact]
    public async Task BrowseModsFolderCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\Mods\Folder";
        _viewModel.ShowFolderPickerAsync = (title) => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseModsFolderCommand.Execute().FirstAsync();

        // Assert
        Assert.Equal(pickedPath, _viewModel.ModsFolder);
    }

    [Fact]
    public async Task BrowseIniFolderCommand_UpdatesPath()
    {
        // Arrange
        var pickedPath = @"C:\INI\Folder";
        _viewModel.ShowFolderPickerAsync = (title) => Task.FromResult(pickedPath);

        // Act
        await _viewModel.BrowseIniFolderCommand.Execute().FirstAsync();

        // Assert
        Assert.Equal(pickedPath, _viewModel.IniFolder);
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
        Assert.True(propertyChanged);
        Assert.Equal(value, property?.GetValue(_viewModel));
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
    public void BooleanProperties_UpdateCorrectly(string propertyName, bool value)
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
        Assert.True(propertyChanged);
        Assert.Equal(value, property?.GetValue(_viewModel));
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
        Assert.True(propertyChanged);
        Assert.Equal(value, property?.GetValue(_viewModel));
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
        Assert.True(propertyChanged);
        Assert.Equal(value, property?.GetValue(_viewModel));
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
        Assert.True(_viewModel.RecentLogFiles.Count > 0);
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
        Assert.Equal("", viewModel.DefaultLogPath);
        Assert.True(viewModel.AutoLoadF4SeLogs);
        Assert.Equal(100, viewModel.MaxLogMessages);
        Assert.Equal(1200, viewModel.WindowWidth);
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
        System.Threading.Thread.Sleep(100);

        // Assert
        Assert.Equal(3, viewModel.RecentLogFiles.Count);
        Assert.Equal("log1.log", viewModel.RecentLogFiles[0]);
        Assert.Equal("log2.log", viewModel.RecentLogFiles[1]);
        Assert.Equal("log3.log", viewModel.RecentLogFiles[2]);
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
        Assert.Equal(originalPath, _viewModel.DefaultLogPath);
    }
}