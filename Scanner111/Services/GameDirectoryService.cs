using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for detecting and managing game installation directory and documents directory
/// </summary>
public class GameDirectoryService : IGameDirectoryService
{
    private readonly AppSettings _appSettings;
    private readonly bool _testMode;
    private readonly IYamlSettingsCacheService _yamlSettingsCache;

    public GameDirectoryService(
        IYamlSettingsCacheService yamlSettingsCache,
        AppSettings appSettings,
        bool testMode = false)
    {
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _testMode = testMode;
    }

    /// <summary>
    ///     Gets the current game installation path
    /// </summary>
    public string? GamePath => _appSettings.GamePath;

    /// <summary>
    ///     Gets the current game documents path
    /// </summary>
    public string? DocsPath => _appSettings.DocsPath;

    /// <summary>
    ///     Finds and configures the game installation directory
    /// </summary>
    /// <returns>A string with results or error messages</returns>
    public async Task<string> FindGamePathAsync()
    {
        var results = new StringBuilder();
        results.AppendLine("========= GAME INSTALLATION DIRECTORY DETECTION =========\n");

        // Check if path is already configured
        var existingGamePath = GetSetting<string>(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Root_Folder_Game");
        if (!string.IsNullOrEmpty(existingGamePath) && Directory.Exists(existingGamePath))
        {
            // Validate path by checking if expected executable exists
            var exeName = $"{_appSettings.GameName}{GetVrSuffix()}.exe";
            if (File.Exists(Path.Combine(existingGamePath, exeName)))
            {
                _appSettings.GamePath = existingGamePath;
                results.AppendLine($"✔️ Game path already configured at {existingGamePath}");
                return results.ToString();
            }
        }

        results.AppendLine("Searching for game installation...");

        // Try to find game path from registry
        var registryPath = await FindGamePathFromRegistryAsync();
        if (!string.IsNullOrEmpty(registryPath))
            if (ValidateGamePath(registryPath, out var exePath))
            {
                _appSettings.GamePath = registryPath;
                SaveGamePath(registryPath);
                results.AppendLine($"✔️ Game installation found at {registryPath}");
                return results.ToString();
            }

        // Try to find game path from XSE log file
        var xseLogPath = await FindGamePathFromXseLogAsync();
        if (!string.IsNullOrEmpty(xseLogPath))
            if (ValidateGamePath(xseLogPath, out var exePath))
            {
                _appSettings.GamePath = xseLogPath;
                SaveGamePath(xseLogPath);
                results.AppendLine($"✔️ Game installation found from XSE log at {xseLogPath}");
                return results.ToString();
            }

        // If we got here, we couldn't find the game path
        results.AppendLine("❌ Could not automatically detect game installation directory.");
        results.AppendLine("Please use the 'Browse...' button to set it manually.");
        return results.ToString();
    }

    /// <summary>
    ///     Finds and configures the game documents directory (where config files and logs are stored)
    /// </summary>
    /// <returns>A string with results or error messages</returns>
    public async Task<string> FindDocsPathAsync()
    {
        var results = new StringBuilder();
        results.AppendLine("======== GAME DOCUMENTS DIRECTORY DETECTION =========\n");

        // Check if path is already configured
        var existingDocsPath = GetSetting<string>(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Root_Folder_Docs");
        if (!string.IsNullOrEmpty(existingDocsPath) && Directory.Exists(existingDocsPath))
        {
            _appSettings.DocsPath = existingDocsPath;
            results.AppendLine($"✔️ Documents path already configured at {existingDocsPath}");
            GenerateDocsPaths(existingDocsPath);
            return results.ToString();
        }

        results.AppendLine("Searching for game documents directory...");

        // Try Windows Documents folder first
        var docsPath = await FindDocsPathFromWindowsProfileAsync();
        if (!string.IsNullOrEmpty(docsPath))
            if (Directory.Exists(docsPath))
            {
                _appSettings.DocsPath = docsPath;
                SaveDocsPath(docsPath);
                GenerateDocsPaths(docsPath);
                results.AppendLine($"✔️ Game documents directory found at {docsPath}");
                return results.ToString();
            }

        // If we got here, we couldn't find the docs path
        results.AppendLine("❌ Could not automatically detect game documents directory.");
        results.AppendLine("Please use the 'Browse...' button to set it manually.");
        return results.ToString();
    }

    /// <summary>
    ///     Manually sets the game installation directory
    /// </summary>
    /// <param name="path">The path to set as the game directory</param>
    /// <returns>A result indicating success or failure</returns>
    public async Task<bool> SetGamePathManuallyAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;

        if (!ValidateGamePath(path, out var exePath)) return false;
        _appSettings.GamePath = path;
        SaveGamePath(path);
        GenerateGamePaths(path);
        return true;
    }

    /// <summary>
    ///     Manually sets the game documents directory
    /// </summary>
    /// <param name="path">The path to set as the docs directory</param>
    /// <returns>A result indicating success or failure</returns>
    public async Task<bool> SetDocsPathManuallyAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;

        // For docs path, we'll just ensure it's a directory - validation can be more involved later
        _appSettings.DocsPath = path;
        SaveDocsPath(path);
        GenerateDocsPaths(path);
        return true;
    }

    /// <summary>
    ///     Initializes all required paths for the application
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task InitializePathsAsync()
    {
        await FindGamePathAsync();
        await FindDocsPathAsync();

        // Ensure app directories exist
        EnsureDirectoryExists("CLASSIC Data");
        EnsureDirectoryExists("CLASSIC Backup");
        EnsureDirectoryExists("CLASSIC Backup/Game Files");
        EnsureDirectoryExists("CLASSIC Backup/Cleaned Files");
        EnsureDirectoryExists("CLASSIC Backup/Crash Logs");
    }

    #region Private Helper Methods

    private async Task<string> FindGamePathFromRegistryAsync()
    {
        try
        {
            // Try Bethesda registry key first
            var registryKeyPath =
                $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{_appSettings.GameName}{GetVrSuffix()}";
            using (var key = Registry.LocalMachine.OpenSubKey(registryKeyPath))
            {
                if (key != null)
                {
                    var path = key.GetValue("installed path") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                }
            }

            // Try GOG registry key (for Fallout games)
            if (_appSettings.GameName.Contains("Fallout", StringComparison.OrdinalIgnoreCase))
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
                if (key != null)
                {
                    var path = key.GetValue("path") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                }
            }
        }
        catch (Exception ex)
        {
            // Log exception but continue to other methods
            Console.WriteLine($"Error accessing registry: {ex.Message}");
        }

        return null;
    }

    private async Task<string> FindGamePathFromXseLogAsync()
    {
        try
        {
            // Get XSE log file path
            var xseAcronym = GetSetting<string>(Yaml.Game, $"Game{GetVrSuffix()}_Info.XSE_Acronym");
            var xseLogFile = GetSetting<string>(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Docs_File_XSE");

            if (string.IsNullOrEmpty(xseLogFile) || !File.Exists(xseLogFile)) return null;

            // Read the XSE log file
            var logLines = await File.ReadAllLinesAsync(xseLogFile);
            foreach (var line in logLines)
            {
                if (!line.StartsWith("plugin directory", StringComparison.OrdinalIgnoreCase)) continue;
                // Extract the path from the log line
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                var pluginDirPath = parts[1].Trim();
                // Remove the plugin part of the path to get the game directory
                var gamePath = pluginDirPath.Replace($@"\Data\{xseAcronym}\Plugins", "");

                if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath)) return gamePath;
            }
        }
        catch (Exception ex)
        {
            // Log exception but continue to other methods
            Console.WriteLine($"Error reading XSE log file: {ex.Message}");
        }

        return null;
    }

    private async Task<string> FindDocsPathFromWindowsProfileAsync()
    {
        try
        {
            // Try to get the Documents path from the Windows profile
            var docsName = GetSetting<string>(Yaml.Game, $"Game{GetVrSuffix()}_Info.Main_Docs_Name");
            if (string.IsNullOrEmpty(docsName)) docsName = _appSettings.GameName; // Default to game name

            // Try registry for documents path
            string documentsPath = null;
            using (var key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
            {
                if (key != null) documentsPath = key.GetValue("Personal") as string;
            }

            // Fallback to user profile if registry approach fails
            if (string.IsNullOrEmpty(documentsPath))
                documentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Documents");

            if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
            {
                var fullDocsPath = Path.Combine(documentsPath, "My Games", docsName);
                if (Directory.Exists(fullDocsPath)) return fullDocsPath;
            }
        }
        catch (Exception ex)
        {
            // Log exception but continue
            Console.WriteLine($"Error finding documents path: {ex.Message}");
        }

        return null;
    }

    private bool ValidateGamePath(string path, out string executablePath)
    {
        executablePath = null;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;

        var exeName = $"{_appSettings.GameName}{GetVrSuffix()}.exe";
        executablePath = Path.Combine(path, exeName);

        return File.Exists(executablePath);
    }

    private void SaveGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        try
        {
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Root_Folder_Game", path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving game path: {ex.Message}");
        }
    }

    private void SaveDocsPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        try
        {
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Root_Folder_Docs", path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving docs path: {ex.Message}");
        }
    }

    private void GenerateGamePaths(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) return;

        try
        {
            var xseAcronymBase = GetSetting<string>(Yaml.Game, "Game_Info.XSE_Acronym");
            if (string.IsNullOrEmpty(xseAcronymBase)) xseAcronymBase = "F4SE"; // Default to F4SE

            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_Folder_Data",
                Path.Combine(gamePath, "Data"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_Folder_Scripts",
                Path.Combine(gamePath, "Data", "Scripts"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_Folder_Plugins",
                Path.Combine(gamePath, "Data", xseAcronymBase, "Plugins"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_File_SteamINI",
                Path.Combine(gamePath, "steam_api.ini"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_File_EXE",
                Path.Combine(gamePath, $"{_appSettings.GameName}{GetVrSuffix()}.exe"));

            // Set Address Library path based on game (Fallout4 vs others)
            if (!_appSettings.GameName.Contains("Fallout4", StringComparison.OrdinalIgnoreCase)) return;
            if (GetVrSuffix() == "VR")
            {
                _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_File_AddressLib",
                    Path.Combine(gamePath, "Data", xseAcronymBase, "plugins", "version-1-2-72-0.csv"));
            }
            else
            {
                var gameVersion = GetSetting<string>(Yaml.Game, "game_version") ?? "1-10-163-0";
                _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Game_File_AddressLib",
                    Path.Combine(gamePath, "Data", xseAcronymBase, "plugins", $"version-{gameVersion}.bin"));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating game paths: {ex.Message}");
        }
    }

    private void GenerateDocsPaths(string docsPath)
    {
        if (string.IsNullOrEmpty(docsPath) || !Directory.Exists(docsPath)) return;

        try
        {
            var xseAcronym = GetSetting<string>(Yaml.Game, $"Game{GetVrSuffix()}_Info.XSE_Acronym");
            var xseAcronymBase = GetSetting<string>(Yaml.Game, "Game_Info.XSE_Acronym");

            if (string.IsNullOrEmpty(xseAcronym)) xseAcronym = "F4SE"; // Default

            if (string.IsNullOrEmpty(xseAcronymBase)) xseAcronymBase = "F4SE"; // Default

            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Docs_Folder_XSE",
                Path.Combine(docsPath, xseAcronymBase));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Docs_File_PapyrusLog",
                Path.Combine(docsPath, "Logs", "Script", "Papyrus.0.log"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Docs_File_WryeBashPC",
                Path.Combine(docsPath, "ModChecker.html"));
            _yamlSettingsCache.SetSetting(Yaml.GameLocal, $"Game{GetVrSuffix()}_Info.Docs_File_XSE",
                Path.Combine(docsPath, xseAcronymBase, $"{xseAcronym.ToLowerInvariant()}.log"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating docs paths: {ex.Message}");
        }
    }

    private string GetVrSuffix()
    {
        return _appSettings.VrGameVars;
    }

    private void EnsureDirectoryExists(string directoryPath)
    {
        if (!_testMode && !Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
    }

    private T? GetSetting<T>(Yaml yamlType, string key) where T : class
    {
        try
        {
            return _yamlSettingsCache.GetSetting<T>(yamlType, key);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}