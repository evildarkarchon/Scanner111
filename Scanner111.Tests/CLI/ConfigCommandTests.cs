using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI;

public class ConfigCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;
    private readonly MockCliSettingsService _mockSettingsService;
    private readonly ConfigCommand _command;

    public ConfigCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
        _mockSettingsService = new MockCliSettingsService();
        _command = new ConfigCommand(_mockSettingsService, _messageCapture);
    }

    [Fact]
    public async Task ExecuteAsync_WithShowPath_DisplaysSettingsPath()
    {
        // Arrange
        var options = new ConfigOptions { ShowPath = true };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains(_messageCapture.InfoMessages, msg => msg.Contains("Unified Settings file:"));
        Assert.Contains(_messageCapture.InfoMessages, msg => msg.Contains("File exists:"));
    }

    [Fact]
    public async Task ExecuteAsync_WithReset_ResetsToDefaultSettings()
    {
        // Arrange
        var options = new ConfigOptions { Reset = true };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Resetting configuration to defaults...", _messageCapture.InfoMessages);
        Assert.Contains("Configuration reset to defaults.", _messageCapture.SuccessMessages);
        Assert.True(_mockSettingsService.SaveSettingsCalled);
        var lastSaved = _mockSettingsService.LastSavedSettings;
        Assert.NotNull(lastSaved);
        // Verify it's default settings (all should be default values)
        Assert.False(lastSaved.FcxMode);
        Assert.False(lastSaved.ShowFormIdValues);
    }

    [Fact]
    public async Task ExecuteAsync_WithList_DisplaysCurrentConfiguration()
    {
        // Arrange
        var options = new ConfigOptions { List = true };
        var testSettings = new CliSettings
        {
            FcxMode = true,
            ShowFormIdValues = false,
            SimplifyLogs = true,
            MoveUnsolvedLogs = false,
            CrashLogsDirectory = "/test/path",
            AudioNotifications = true,
            VrMode = false,
            DisableColors = false,
            DisableProgress = true,
            DefaultOutputFormat = "detailed",
            DefaultGamePath = "/game/path",
            DefaultScanDirectory = "/scan/path"
        };
        _mockSettingsService.SetTestSettings(testSettings);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Current Scanner111 Configuration:", _messageCapture.InfoMessages);
        Assert.Contains("FCX Mode: True", _messageCapture.InfoMessages);
        Assert.Contains("Show FormID Values: False", _messageCapture.InfoMessages);
        Assert.Contains("Simplify Logs: True", _messageCapture.InfoMessages);
        Assert.Contains("Move Unsolved Logs: False", _messageCapture.InfoMessages);
        Assert.Contains("Crash Logs Directory: /test/path", _messageCapture.InfoMessages);
        Assert.Contains("Audio Notifications: True", _messageCapture.InfoMessages);
        Assert.Contains("VR Mode: False", _messageCapture.InfoMessages);
        Assert.Contains("Disable Colors: False", _messageCapture.InfoMessages);
        Assert.Contains("Disable Progress: True", _messageCapture.InfoMessages);
        Assert.Contains("Default Output Format: detailed", _messageCapture.InfoMessages);
        Assert.Contains("Default Game Path: /game/path", _messageCapture.InfoMessages);
        Assert.Contains("Default Scan Directory: /scan/path", _messageCapture.InfoMessages);
        Assert.Contains("Enable Update Check: True", _messageCapture.InfoMessages);
        Assert.Contains("Update Source: Both", _messageCapture.InfoMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidSet_SavesSetting()
    {
        // Arrange
        var options = new ConfigOptions { Set = "FcxMode=true" };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Set FcxMode = true", _messageCapture.SuccessMessages);
        Assert.Contains("Setting saved to configuration file.", _messageCapture.InfoMessages);
        Assert.True(_mockSettingsService.SaveSettingAsyncCalled);
        Assert.Equal("FcxMode", _mockSettingsService.LastSavedKey);
        Assert.Equal("true", _mockSettingsService.LastSavedValue);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidSetFormat_ReturnsError()
    {
        // Arrange
        var options = new ConfigOptions { Set = "InvalidFormat" };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(1, result);
        Assert.Contains("Invalid set format. Use: --set \"key=value\"", _messageCapture.ErrorMessages);
        Assert.Contains("Available settings:", _messageCapture.InfoMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidSetting_ReturnsError()
    {
        // Arrange
        var options = new ConfigOptions { Set = "InvalidKey=value" };
        _mockSettingsService.ThrowOnInvalidKey = true;

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(1, result);
        Assert.Contains("Unknown setting: InvalidKey", _messageCapture.ErrorMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleOptions_ExecutesInOrder()
    {
        // Arrange
        var options = new ConfigOptions 
        { 
            ShowPath = true,
            List = true,
            Set = "FcxMode=true"
        };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        // Verify all operations were performed
        Assert.Contains(_messageCapture.InfoMessages, msg => msg.Contains("Unified Settings file:"));
        Assert.Contains("Current Scanner111 Configuration:", _messageCapture.InfoMessages);
        Assert.Contains("Set FcxMode = true", _messageCapture.SuccessMessages);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }

    private class MockCliSettingsService : ICliSettingsService
    {
        private CliSettings _testSettings = new();
        
        public bool SaveSettingsCalled { get; private set; }
        public ApplicationSettings? LastSavedSettings { get; private set; }
        public bool SaveSettingAsyncCalled { get; private set; }
        public string? LastSavedKey { get; private set; }
        public object? LastSavedValue { get; private set; }
        public bool ThrowOnInvalidKey { get; set; }

        public void SetTestSettings(CliSettings settings)
        {
            _testSettings = settings;
        }

        public Task<ApplicationSettings> LoadSettingsAsync()
        {
            var appSettings = new ApplicationSettings();
            // Map CliSettings to ApplicationSettings
            appSettings.FcxMode = _testSettings.FcxMode;
            appSettings.ShowFormIdValues = _testSettings.ShowFormIdValues;
            appSettings.SimplifyLogs = _testSettings.SimplifyLogs;
            appSettings.MoveUnsolvedLogs = _testSettings.MoveUnsolvedLogs;
            appSettings.CrashLogsDirectory = _testSettings.CrashLogsDirectory;
            appSettings.AudioNotifications = _testSettings.AudioNotifications;
            appSettings.VrMode = _testSettings.VrMode;
            appSettings.DisableColors = _testSettings.DisableColors;
            appSettings.DisableProgress = _testSettings.DisableProgress;
            appSettings.DefaultOutputFormat = _testSettings.DefaultOutputFormat;
            appSettings.DefaultGamePath = _testSettings.DefaultGamePath;
            appSettings.DefaultScanDirectory = _testSettings.DefaultScanDirectory;
            appSettings.EnableUpdateCheck = true; // Default value from ApplicationSettings
            appSettings.UpdateSource = "Both"; // Default value from ApplicationSettings
            return Task.FromResult(appSettings);
        }

        public Task SaveSettingsAsync(ApplicationSettings settings)
        {
            SaveSettingsCalled = true;
            LastSavedSettings = settings;
            return Task.CompletedTask;
        }

        public Task SaveSettingAsync(string key, object value)
        {
            SaveSettingAsyncCalled = true;
            LastSavedKey = key;
            LastSavedValue = value;
            
            if (ThrowOnInvalidKey && key == "InvalidKey")
            {
                throw new ArgumentException($"Unknown setting: {key}");
            }
            
            return Task.CompletedTask;
        }

        public ApplicationSettings GetDefaultSettings()
        {
            return new ApplicationSettings();
        }

        public Task<CliSettings> LoadCliSettingsAsync()
        {
            return Task.FromResult(_testSettings);
        }

        public Task SaveCliSettingsAsync(CliSettings settings)
        {
            return Task.CompletedTask;
        }
    }
}