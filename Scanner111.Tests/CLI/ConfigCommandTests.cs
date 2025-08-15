using FluentAssertions;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.CLI;

[Collection("Terminal UI Tests")]
public class ConfigCommandTests : IDisposable
{
    private readonly ConfigCommand _command;
    private readonly TestMessageCapture _messageCapture;
    private readonly MockCliSettingsService _mockSettingsService;

    public ConfigCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
        _mockSettingsService = new MockCliSettingsService();
        _command = new ConfigCommand(_mockSettingsService, _messageCapture);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }

    [Fact]
    public async Task ExecuteAsync_WithShowPath_DisplaysSettingsPath()
    {
        // Arrange
        var options = new ConfigOptions { ShowPath = true };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0, "because the command should succeed");
        _messageCapture.InfoMessages.Should().Contain(msg => msg.Contains("Unified Settings file:"),
            "because the settings path should be displayed");
        _messageCapture.InfoMessages.Should().Contain(msg => msg.Contains("File exists:"),
            "because the file existence status should be shown");
    }

    [Fact]
    public async Task ExecuteAsync_WithReset_ResetsToDefaultSettings()
    {
        // Arrange
        var options = new ConfigOptions { Reset = true };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0, "because the reset should succeed");
        _messageCapture.InfoMessages.Should().Contain("Resetting configuration to defaults...",
            "because reset progress should be shown");
        _messageCapture.SuccessMessages.Should().Contain("Configuration reset to defaults.",
            "because success should be reported");
        _mockSettingsService.SaveSettingsCalled.Should().BeTrue("because settings should be saved after reset");
        var lastSaved = _mockSettingsService.LastSavedSettings;
        lastSaved.Should().NotBeNull("because settings should have been saved");
        // Verify it's default settings (all should be default values)
        lastSaved.FcxMode.Should().BeFalse("because FCX mode should be disabled by default");
        lastSaved.ShowFormIdValues.Should().BeFalse("because FormID values should be hidden by default");
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
        result.Should().Be(0, "because listing configuration should succeed");
        _messageCapture.InfoMessages.Should().Contain("Current Scanner111 Configuration:",
            "because configuration header should be shown");
        _messageCapture.InfoMessages.Should().Contain("FCX Mode: True");
        _messageCapture.InfoMessages.Should().Contain("Show FormID Values: False");
        _messageCapture.InfoMessages.Should().Contain("Simplify Logs: True");
        _messageCapture.InfoMessages.Should().Contain("Move Unsolved Logs: False");
        _messageCapture.InfoMessages.Should().Contain("Crash Logs Directory: /test/path");
        _messageCapture.InfoMessages.Should().Contain("Audio Notifications: True");
        _messageCapture.InfoMessages.Should().Contain("VR Mode: False");
        _messageCapture.InfoMessages.Should().Contain("Disable Colors: False");
        _messageCapture.InfoMessages.Should().Contain("Disable Progress: True");
        _messageCapture.InfoMessages.Should().Contain("Default Output Format: detailed");
        _messageCapture.InfoMessages.Should().Contain("Default Game Path: /game/path");
        _messageCapture.InfoMessages.Should().Contain("Default Scan Directory: /scan/path");
        _messageCapture.InfoMessages.Should().Contain("Enable Update Check: True");
        _messageCapture.InfoMessages.Should().Contain("Update Source: Both");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidSet_SavesSetting()
    {
        // Arrange
        var options = new ConfigOptions { Set = "FcxMode=true" };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0, "because setting a valid value should succeed");
        _messageCapture.SuccessMessages.Should().Contain("Set FcxMode = true",
            "because the set operation should be confirmed");
        _messageCapture.InfoMessages.Should().Contain("Setting saved to configuration file.",
            "because saving should be reported");
        _mockSettingsService.SaveSettingAsyncCalled.Should().BeTrue("because the setting should be saved");
        _mockSettingsService.LastSavedKey.Should().Be("FcxMode", "because the correct key should be saved");
        _mockSettingsService.LastSavedValue.Should().Be("true", "because the correct value should be saved");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidSetFormat_ReturnsError()
    {
        // Arrange
        var options = new ConfigOptions { Set = "InvalidFormat" };

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(1, "because invalid format should fail");
        _messageCapture.ErrorMessages.Should().Contain("Invalid set format. Use: --set \"key=value\"",
            "because error message should explain the correct format");
        _messageCapture.InfoMessages.Should().Contain("Available settings:",
            "because available settings should be shown after error");
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
        result.Should().Be(1, "because invalid setting key should fail");
        _messageCapture.ErrorMessages.Should().Contain("Unknown setting: InvalidKey",
            "because error should indicate the unknown setting");
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
        result.Should().Be(0, "because all operations should succeed");
        // Verify all operations were performed
        _messageCapture.InfoMessages.Should().Contain(msg => msg.Contains("Unified Settings file:"),
            "because path should be shown");
        _messageCapture.InfoMessages.Should().Contain("Current Scanner111 Configuration:",
            "because configuration should be listed");
        _messageCapture.SuccessMessages.Should().Contain("Set FcxMode = true",
            "because setting should be applied");
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

            if (ThrowOnInvalidKey && key == "InvalidKey") throw new ArgumentException($"Unknown setting: {key}");

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

        public void SetTestSettings(CliSettings settings)
        {
            _testSettings = settings;
        }
    }
}