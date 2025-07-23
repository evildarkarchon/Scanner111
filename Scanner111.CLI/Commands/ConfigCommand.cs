using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class ConfigCommand : ICommand<ConfigOptions>
{
    private readonly ICliSettingsService _settingsService;

    public ConfigCommand(ICliSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Executes the configuration command asynchronously based on the specified options.
    /// </summary>
    /// <param name="options">The configuration options provided to the command, which determine the operation to be performed. This includes listing the current configuration, resetting to defaults, showing the configuration file path, or setting a configuration value.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an integer that indicates the exit code of the command, where 0 typically signifies success.
    /// </returns>
    public async Task<int> ExecuteAsync(ConfigOptions options)
    {
        var messageHandler = new CliMessageHandler();
        MessageHandler.Initialize(messageHandler);

        if (options.ShowPath)
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Scanner111", "settings.json");
            MessageHandler.MsgInfo($"Unified Settings file: {settingsPath}");
            MessageHandler.MsgInfo($"File exists: {File.Exists(settingsPath)}");
        }

        if (options.Reset)
        {
            MessageHandler.MsgInfo("Resetting configuration to defaults...");
            var defaultSettings = _settingsService.GetDefaultSettings();
            await _settingsService.SaveSettingsAsync(defaultSettings);
            MessageHandler.MsgSuccess("Configuration reset to defaults.");
            return 0;
        }

        if (options.List) await ListCurrentConfiguration();

        if (!string.IsNullOrEmpty(options.Set)) return await SetConfiguration(options.Set);

        return 0;
    }

    private async Task ListCurrentConfiguration()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        MessageHandler.MsgInfo("Current Scanner111 Configuration:");
        MessageHandler.MsgInfo("================================");
        MessageHandler.MsgInfo($"FCX Mode: {settings.FcxMode}");
        MessageHandler.MsgInfo($"Show FormID Values: {settings.ShowFormIdValues}");
        MessageHandler.MsgInfo($"Simplify Logs: {settings.SimplifyLogs}");
        MessageHandler.MsgInfo($"Move Unsolved Logs: {settings.MoveUnsolvedLogs}");
        MessageHandler.MsgInfo($"Crash Logs Directory: {settings.CrashLogsDirectory}");
        MessageHandler.MsgInfo($"Audio Notifications: {settings.AudioNotifications}");
        MessageHandler.MsgInfo($"VR Mode: {settings.VrMode}");
        MessageHandler.MsgInfo($"Disable Colors: {settings.DisableColors}");
        MessageHandler.MsgInfo($"Disable Progress: {settings.DisableProgress}");
        MessageHandler.MsgInfo($"Default Output Format: {settings.DefaultOutputFormat}");
        MessageHandler.MsgInfo($"Default Game Path: {settings.DefaultGamePath}");
        MessageHandler.MsgInfo($"Default Scan Directory: {settings.DefaultScanDirectory}");
        MessageHandler.MsgInfo($"Enable Update Check: {settings.EnableUpdateCheck}");
        MessageHandler.MsgInfo($"Update Source: {settings.UpdateSource}");
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

                MessageHandler.MsgSuccess($"Set {key} = {value}");
                MessageHandler.MsgInfo("Setting saved to configuration file.");
            }
            catch (ArgumentException ex)
            {
                MessageHandler.MsgError(ex.Message);
                return 1;
            }
        }
        else
        {
            MessageHandler.MsgError("Invalid set format. Use: --set \"key=value\"");
            MessageHandler.MsgInfo("Available settings:");
            MessageHandler.MsgInfo("  FcxMode, ShowFormIdValues, SimplifyLogs, MoveUnsolvedLogs");
            MessageHandler.MsgInfo("  AudioNotifications, VrMode, DisableColors, DisableProgress");
            MessageHandler.MsgInfo("  DefaultOutputFormat, DefaultGamePath, DefaultScanDirectory, CrashLogsDirectory");
            MessageHandler.MsgInfo("  EnableUpdateCheck, UpdateSource");
            return 1;
        }

        return 0;
    }
}