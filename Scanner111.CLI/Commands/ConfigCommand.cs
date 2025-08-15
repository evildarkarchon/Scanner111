using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class ConfigCommand : ICommand<ConfigOptions>
{
    private readonly IMessageHandler _messageHandler;
    private readonly ICliSettingsService _settingsService;

    public ConfigCommand(ICliSettingsService settingsService, IMessageHandler messageHandler)
    {
        _settingsService = Guard.NotNull(settingsService, nameof(settingsService));
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
    }

    /// <summary>
    ///     Executes the configuration command asynchronously based on the specified options.
    /// </summary>
    /// <param name="options">
    ///     The configuration options provided to the command, which determine the operation to be performed.
    ///     This includes listing the current configuration, resetting to defaults, showing the configuration file path, or
    ///     setting a configuration value.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains an integer that indicates the exit code of
    ///     the command, where 0 typically signifies success.
    /// </returns>
    public async Task<int> ExecuteAsync(ConfigOptions options)
    {
        if (options.ShowPath)
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Scanner111", "settings.json");
            _messageHandler.ShowInfo($"Unified Settings file: {settingsPath}");
            _messageHandler.ShowInfo($"File exists: {File.Exists(settingsPath)}");
        }

        if (options.Reset)
        {
            _messageHandler.ShowInfo("Resetting configuration to defaults...");
            var defaultSettings = _settingsService.GetDefaultSettings();
            await _settingsService.SaveSettingsAsync(defaultSettings);
            _messageHandler.ShowSuccess("Configuration reset to defaults.");
            return 0;
        }

        if (options.List) await ListCurrentConfiguration();

        if (!string.IsNullOrEmpty(options.Set)) return await SetConfiguration(options.Set);

        return 0;
    }

    private async Task ListCurrentConfiguration()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        _messageHandler.ShowInfo("Current Scanner111 Configuration:");
        _messageHandler.ShowInfo("================================");
        _messageHandler.ShowInfo($"FCX Mode: {settings.FcxMode}");
        _messageHandler.ShowInfo($"Show FormID Values: {settings.ShowFormIdValues}");
        _messageHandler.ShowInfo($"Simplify Logs: {settings.SimplifyLogs}");
        _messageHandler.ShowInfo($"Move Unsolved Logs: {settings.MoveUnsolvedLogs}");
        _messageHandler.ShowInfo($"Crash Logs Directory: {settings.CrashLogsDirectory}");
        _messageHandler.ShowInfo($"Audio Notifications: {settings.AudioNotifications}");
        _messageHandler.ShowInfo($"VR Mode: {settings.VrMode}");
        _messageHandler.ShowInfo($"Disable Colors: {settings.DisableColors}");
        _messageHandler.ShowInfo($"Disable Progress: {settings.DisableProgress}");
        _messageHandler.ShowInfo($"Default Output Format: {settings.DefaultOutputFormat}");
        _messageHandler.ShowInfo($"Default Game Path: {settings.DefaultGamePath}");
        _messageHandler.ShowInfo($"Default Scan Directory: {settings.DefaultScanDirectory}");
        _messageHandler.ShowInfo($"Enable Update Check: {settings.EnableUpdateCheck}");
        _messageHandler.ShowInfo($"Update Source: {settings.UpdateSource}");
    }

    private async Task<int> SetConfiguration(string setOption)
    {
        var parts = setOption.Split('=');
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            try
            {
                // Save to settings file
                await _settingsService.SaveSettingAsync(key, value);

                _messageHandler.ShowSuccess($"Set {key} = {value}");
                _messageHandler.ShowInfo("Setting saved to configuration file.");
            }
            catch (ArgumentException ex)
            {
                _messageHandler.ShowError(ex.Message);
                return 1;
            }
        }
        else
        {
            _messageHandler.ShowError("Invalid set format. Use: --set \"key=value\"");
            _messageHandler.ShowInfo("Available settings:");
            _messageHandler.ShowInfo("  FcxMode, ShowFormIdValues, SimplifyLogs, MoveUnsolvedLogs");
            _messageHandler.ShowInfo("  AudioNotifications, VrMode, DisableColors, DisableProgress");
            _messageHandler.ShowInfo(
                "  DefaultOutputFormat, DefaultGamePath, DefaultScanDirectory, CrashLogsDirectory");
            _messageHandler.ShowInfo("  EnableUpdateCheck, UpdateSource");
            return 1;
        }

        return 0;
    }
}