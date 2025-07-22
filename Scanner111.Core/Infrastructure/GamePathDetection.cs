using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides functionality to detect the installation path of a game using various approaches,
/// including registry checks, log file parsing, and common installation path scanning.
/// </summary>
public static class GamePathDetection
{
    /// <summary>
    /// Attempts to detect the game installation path using various detection methods.
    /// </summary>
    /// <returns>The detected game installation path or an empty string if detection fails.</returns>
    public static string TryDetectGamePath()
    {
        // Try registry detection first (Windows only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPath = TryGetGamePathFromRegistry();
            if (!string.IsNullOrEmpty(registryPath))
                return registryPath;
        }

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
    /// Attempts to retrieve the installation path of the game from the Windows registry.
    /// </summary>
    /// <returns>The detected game installation path from the registry or an empty string if no registry entry is found or valid.</returns>
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

            // Try Fallout4VR if regular Fallout4 not found
            using var regKeyVr =
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4VR");
            if (regKeyVr?.GetValue("installed path") is string bethesdaVrPath && ValidateGamePath(bethesdaVrPath))
                return bethesdaVrPath;

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
    /// Attempts to retrieve the game installation path by parsing XSE (Script Extender) log files.
    /// </summary>
    /// <returns>The detected game installation path from the XSE log files or an empty string if the path cannot be determined.</returns>
    public static string TryGetGamePathFromXseLog()
    {
        try
        {
            // Check for F4SE/SKSE log files in Documents
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var xsePaths = new[]
            {
                Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE", "f4se.log"),
                Path.Combine(documentsPath, "My Games", "Fallout4VR", "F4SE", "f4se.log"),
                Path.Combine(documentsPath, "My Games", "Skyrim Special Edition", "SKSE", "skse64.log"),
                Path.Combine(documentsPath, "My Games", "Skyrim", "SKSE", "skse.log")
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
    /// Validates whether the specified path contains a valid game installation by checking for required game files.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <returns>True if the directory contains necessary game files; otherwise, false.</returns>
    public static bool ValidateGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // Check for game executables
        var executables = new[] { "Fallout4.exe", "Fallout4VR.exe", "SkyrimSE.exe", "Skyrim.exe" };
        return executables.Any(exe => File.Exists(Path.Combine(path, exe)));
    }
}