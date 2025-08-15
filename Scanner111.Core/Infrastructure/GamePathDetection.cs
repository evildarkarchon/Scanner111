using System.Data.SQLite;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Provides functionality to detect the installation path of a game using various approaches,
///     including registry checks, log file parsing, and common installation path scanning.
/// </summary>
public static class GamePathDetection
{
    /// <summary>
    ///     Attempts to detect the game installation path using various detection methods.
    /// </summary>
    /// <returns>The detected game installation path or an empty string if detection fails.</returns>
    public static string TryDetectGamePath()
    {
        return TryDetectGamePath("Fallout4");
    }

    /// <summary>
    ///     Attempts to detect the game installation path for a specific game.
    /// </summary>
    /// <param name="gameType">The game type to detect (e.g., "Fallout4", "Skyrim")</param>
    /// <returns>The detected game installation path or an empty string if detection fails.</returns>
    public static string TryDetectGamePath(string gameType)
    {
        // Try registry detection first (Windows only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPath = TryGetGamePathFromRegistry();
            if (!string.IsNullOrEmpty(registryPath))
                return registryPath;
        }

        // Try Steam detection
        var steamPath = TryGetGamePathFromSteam(gameType);
        if (!string.IsNullOrEmpty(steamPath))
            return steamPath;

        // Try GOG detection
        var gogPath = TryGetGamePathFromGOG(gameType);
        if (!string.IsNullOrEmpty(gogPath))
            return gogPath;

        // Try Epic Games detection
        var epicPath = TryGetGamePathFromEpic(gameType);
        if (!string.IsNullOrEmpty(epicPath))
            return epicPath;

        // Try XSE log file parsing
        var xseLogPath = TryGetGamePathFromXseLog();
        if (!string.IsNullOrEmpty(xseLogPath))
            return xseLogPath;

        // Check common installation paths as fallback
        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4",
            @"C:\Program Files\Steam\steamapps\common\Fallout 4",
            @"C:\Steam\steamapps\common\Fallout 4",
            @"D:\Steam\steamapps\common\Fallout 4",
            @"C:\GOG Games\Fallout 4",
            @"C:\Games\Fallout 4",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bethesda.net Launcher",
                "games", "Fallout4")
        };

        foreach (var path in commonPaths)
            if (ValidateGamePath(path))
                return path;

        return "";
    }

    /// <summary>
    ///     Attempts to retrieve the installation path of the game from the Windows registry.
    /// </summary>
    /// <returns>
    ///     The detected game installation path from the registry or an empty string if no registry entry is found or
    ///     valid.
    /// </returns>
    public static string TryGetGamePathFromRegistry()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";

        try
        {
            // Try Bethesda Softworks registry key first
            using var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4");
            if (regKey?.GetValue("installed path") is string bethesdaPath && ValidateGamePath(bethesdaPath))
                return bethesdaPath;

            // Try GOG registry key
            using var regKeyGog = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
            if (regKeyGog?.GetValue("path") is string gogPath && ValidateGamePath(gogPath)) return gogPath;
        }
        catch
        {
            // Registry access failed, continue to other methods
        }

        return "";
    }

    /// <summary>
    ///     Attempts to retrieve the game installation path by parsing XSE (Script Extender) log files.
    /// </summary>
    /// <returns>
    ///     The detected game installation path from the XSE log files or an empty string if the path cannot be
    ///     determined.
    /// </returns>
    public static string TryGetGamePathFromXseLog()
    {
        try
        {
            // Check for F4SE/SKSE log files in Documents
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var xsePaths = new[]
            {
                Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE", "f4se.log")
            };

            foreach (var logPath in xsePaths)
                if (File.Exists(logPath))
                {
                    var lines = File.ReadAllLines(logPath);
                    foreach (var line in lines)
                        if (line.StartsWith("plugin directory"))
                        {
                            // Extract path: "plugin directory = C:\Steam\steamapps\common\Fallout 4\Data\F4SE\Plugins"
                            var parts = line.Split('=', 2);
                            if (parts.Length != 2) continue;
                            var fullPath = parts[1].Trim();
                            // Remove "\Data\F4SE\Plugins" or "\Data\SKSE\Plugins" suffix to get game root
                            var gamePath = fullPath
                                .Replace(@"\Data\F4SE\Plugins", "")
                                .Replace(@"\Data\SKSE\Plugins", "")
                                .Replace(@"\Data\SKSE64\Plugins", "");

                            if (ValidateGamePath(gamePath)) return gamePath;
                        }
                }
        }
        catch
        {
            // XSE log parsing failed, continue to other methods
        }

        return "";
    }

    /// <summary>
    ///     Validates whether the specified path contains a valid game installation by checking for required game files.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <returns>True if the directory contains necessary game files; otherwise, false.</returns>
    public static bool ValidateGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // Check for game executables - Fallout 4 only
        var executables = new[] { "Fallout4.exe" };
        return executables.Any(exe => File.Exists(Path.Combine(path, exe)));
    }

    /// <summary>
    ///     Attempts to detect game installation from Steam
    /// </summary>
    private static string TryGetGamePathFromSteam(string gameType)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";

        try
        {
            // Try to find Steam installation
            using var steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ??
                                 Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

            if (steamKey?.GetValue("InstallPath") is string steamPath)
            {
                // Check libraryfolders.vdf for all Steam library locations
                var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    var content = File.ReadAllText(libraryFoldersPath);
                    var paths = ExtractSteamLibraryPaths(content);
                    paths.Insert(0, steamPath); // Add main Steam path

                    foreach (var libPath in paths)
                    {
                        var gamePath = Path.Combine(libPath, "steamapps", "common", "Fallout 4");
                        if (ValidateGamePath(gamePath))
                            return gamePath;
                    }
                }
            }
        }
        catch
        {
            // Steam detection failed
        }

        return "";
    }

    /// <summary>
    ///     Attempts to detect game installation from GOG Galaxy
    /// </summary>
    private static string TryGetGamePathFromGOG(string gameType)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";

        try
        {
            // First try registry approach for GOG
            using var regKeyGog = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
            if (regKeyGog?.GetValue("path") is string gogRegPath && ValidateGamePath(gogRegPath))
                return gogRegPath;

            // Check GOG Galaxy 2.0 database
            var gogDbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "GOG.com", "Galaxy", "storage", "galaxy-2.0.db"
            );

            if (File.Exists(gogDbPath))
            {
                using var connection = new SQLiteConnection($"Data Source={gogDbPath};Version=3;Read Only=True;");
                connection.Open();

                // Query for Fallout 4 installation
                // GOG Galaxy stores game info in multiple tables
                const string query = @"
                    SELECT p.installLocation 
                    FROM InstalledProducts p
                    INNER JOIN LimitedDetails d ON p.productId = d.productId
                    WHERE d.title LIKE '%Fallout 4%' 
                       OR p.productId = '1998527297'
                    LIMIT 1";

                using var command = new SQLiteCommand(query, connection);
                var installPath = command.ExecuteScalar() as string;

                if (!string.IsNullOrEmpty(installPath) && ValidateGamePath(installPath))
                    return installPath;

                // Try alternative query structure if first one fails
                const string altQuery = @"
                    SELECT installLocation 
                    FROM PlayTasks
                    WHERE gameId = '1998527297' 
                       OR name LIKE '%Fallout 4%'
                    LIMIT 1";

                using var altCommand = new SQLiteCommand(altQuery, connection);
                var altPath = altCommand.ExecuteScalar() as string;

                if (!string.IsNullOrEmpty(altPath) && ValidateGamePath(altPath))
                    return altPath;
            }
        }
        catch
        {
            // GOG detection failed - database might be locked or structure changed
        }

        return "";
    }

    /// <summary>
    ///     Attempts to detect game installation from Epic Games Launcher
    /// </summary>
    private static string TryGetGamePathFromEpic(string gameType)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";

        try
        {
            var epicManifestsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicManifestsPath))
                // Epic stores game info in JSON manifest files
                foreach (var manifestFile in Directory.GetFiles(epicManifestsPath, "*.item"))
                {
                    var content = File.ReadAllText(manifestFile);
                    if (content.Contains("Fallout 4") || content.Contains("Fallout4"))
                    {
                        // Parse JSON to extract InstallLocation
                        var installMatch = Regex.Match(
                            content,
                            @"""InstallLocation""\s*:\s*""([^""]+)"""
                        );

                        if (installMatch.Success)
                        {
                            var path = installMatch.Groups[1].Value.Replace(@"\\", @"\");
                            if (ValidateGamePath(path))
                                return path;
                        }
                    }
                }
        }
        catch
        {
            // Epic detection failed
        }

        return "";
    }

    /// <summary>
    ///     Extracts Steam library paths from libraryfolders.vdf content
    /// </summary>
    private static List<string> ExtractSteamLibraryPaths(string vdfContent)
    {
        var paths = new List<string>();

        try
        {
            // Simple regex to extract paths from VDF format
            var matches = Regex.Matches(
                vdfContent,
                @"""path""\s*""([^""]+)"""
            );

            foreach (Match match in matches)
                if (match.Success)
                {
                    var path = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                        paths.Add(path);
                }
        }
        catch
        {
            // Path extraction failed
        }

        return paths;
    }

    /// <summary>
    ///     Gets the Documents folder path for game INI files
    /// </summary>
    public static string GetGameDocumentsPath(string gameType)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return gameType switch
        {
            "Fallout4" => Path.Combine(documentsPath, "My Games", "Fallout4"),
            "Skyrim" => Path.Combine(documentsPath, "My Games", "Skyrim Special Edition"),
            _ => ""
        };
    }

    /// <summary>
    ///     Detects game configuration including paths and version info
    /// </summary>
    public static GameConfiguration? DetectGameConfiguration(string gameType = "Fallout4")
    {
        var gamePath = TryDetectGamePath(gameType);
        if (string.IsNullOrEmpty(gamePath))
            return null;

        var config = new GameConfiguration
        {
            GameName = gameType,
            RootPath = gamePath,
            ExecutablePath = Path.Combine(gamePath, gameType + ".exe"),
            DocumentsPath = GetGameDocumentsPath(gameType),
            Platform = DetectPlatform(gamePath)
        };

        // Detect XSE
        var xseExe = gameType == "Fallout4" ? "f4se_loader.exe" : "skse64_loader.exe";
        var xsePath = Path.Combine(gamePath, xseExe);
        if (File.Exists(xsePath)) config.XsePath = xsePath;

        return config;
    }

    /// <summary>
    ///     Detects the platform (Steam, GOG, etc.) from the game path
    /// </summary>
    private static string DetectPlatform(string gamePath)
    {
        if (gamePath.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
            return "Steam";
        if (gamePath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            return "GOG";
        if (gamePath.Contains("Epic", StringComparison.OrdinalIgnoreCase))
            return "Epic";
        if (gamePath.Contains("Bethesda.net", StringComparison.OrdinalIgnoreCase))
            return "Bethesda.net";

        return "Unknown";
    }
}