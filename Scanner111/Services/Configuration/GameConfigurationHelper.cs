using System.Threading.Tasks;

namespace Scanner111.Services.Configuration;

/// <summary>
/// Helper class for game-specific configuration operations
/// </summary>
public class GameConfigurationHelper
{
    private readonly IConfigurationService _config;

    public GameConfigurationHelper(IConfigurationService config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets the current managed game
    /// </summary>
    public string GetManagedGame()
    {
        return _config.GetSetting("Managed Game", "Fallout 4");
    }

    /// <summary>
    /// Gets whether VR mode is enabled
    /// </summary>
    public bool IsVrMode()
    {
        return _config.GetSetting("VR Mode", false);
    }

    /// <summary>
    /// Gets the appropriate game info key based on VR mode
    /// </summary>
    public string GetGameInfoKey()
    {
        return IsVrMode() ? "GameVR_Info" : "Game_Info";
    }

    /// <summary>
    /// Gets game-specific folder paths
    /// </summary>
    public async Task<GamePaths> GetGamePathsAsync()
    {
        var gameInfoKey = GetGameInfoKey();

        return new GamePaths
        {
            GameFolder = await _config.GetValueAsync<string>(YamlStore.GameLocal, $"{gameInfoKey}.Root_Folder_Game"),
            DocsFolder = await _config.GetValueAsync<string>(YamlStore.GameLocal, $"{gameInfoKey}.Root_Folder_Docs"),
            DataFolder = await _config.GetValueAsync<string>(YamlStore.GameLocal, $"{gameInfoKey}.Game_Folder_Data"),
            PluginsFolder =
                await _config.GetValueAsync<string>(YamlStore.GameLocal, $"{gameInfoKey}.Game_Folder_Plugins"),
            ScriptsFolder =
                await _config.GetValueAsync<string>(YamlStore.GameLocal, $"{gameInfoKey}.Game_Folder_Scripts")
        };
    }

    /// <summary>
    /// Gets user-configured paths
    /// </summary>
    public UserPaths GetUserPaths()
    {
        return new UserPaths
        {
            IniPath = _config.GetSetting<string>("INI Folder Path"),
            ModsPath = _config.GetSetting<string>("MODS Folder Path"),
            CustomScanPath = _config.GetSetting<string>("SCAN Custom Path")
        };
    }

    /// <summary>
    /// Gets scanner settings
    /// </summary>
    public ScannerSettings GetScannerSettings()
    {
        return new ScannerSettings
        {
            FcxMode = _config.GetSetting("FCX Mode", false),
            SimplifyLogs = _config.GetSetting("Simplify Logs", false),
            UpdateCheck = _config.GetSetting("Update Check", true),
            ShowFormIdValues = _config.GetSetting("Show FormID Values", true),
            MoveUnsolvedLogs = _config.GetSetting("Move Unsolved Logs", true),
            AudioNotifications = _config.GetSetting("Audio Notifications", true),
            UpdateSource = _config.GetSetting("Update Source", "Both")
        };
    }

    /// <summary>
    /// Updates scanner settings
    /// </summary>
    public async Task UpdateScannerSettingsAsync(ScannerSettings settings)
    {
        await _config.SetSettingAsync("FCX Mode", settings.FcxMode);
        await _config.SetSettingAsync("Simplify Logs", settings.SimplifyLogs);
        await _config.SetSettingAsync("Update Check", settings.UpdateCheck);
        await _config.SetSettingAsync("Show FormID Values", settings.ShowFormIdValues);
        await _config.SetSettingAsync("Move Unsolved Logs", settings.MoveUnsolvedLogs);
        await _config.SetSettingAsync("Audio Notifications", settings.AudioNotifications);
        await _config.SetSettingAsync("Update Source", settings.UpdateSource);
    }
}

/// <summary>
/// Represents game-specific paths
/// </summary>
public class GamePaths
{
    public string? GameFolder { get; set; }
    public string? DocsFolder { get; set; }
    public string? DataFolder { get; set; }
    public string? PluginsFolder { get; set; }
    public string? ScriptsFolder { get; set; }
}

/// <summary>
/// Represents user-configured paths
/// </summary>
public class UserPaths
{
    public string? IniPath { get; set; }
    public string? ModsPath { get; set; }
    public string? CustomScanPath { get; set; }
}

/// <summary>
/// Represents scanner settings
/// </summary>
public class ScannerSettings
{
    public bool FcxMode { get; set; }
    public bool SimplifyLogs { get; set; }
    public bool UpdateCheck { get; set; }
    public bool ShowFormIdValues { get; set; }
    public bool MoveUnsolvedLogs { get; set; }
    public bool AudioNotifications { get; set; }
    public string UpdateSource { get; set; } = "Both";
}