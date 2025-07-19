using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides game path detection functionality using registry and XSE log parsing
/// </summary>
public static class GamePathDetection
{
    /// <summary>
    /// Attempts to detect the game installation path using multiple methods
    /// </summary>
    /// <returns>The detected game path or empty string if not found</returns>
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
        var xseLogPath = TryGetGamePathFromXSELog();
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bethesda.net Launcher", "games", "Fallout4")
        };
        
        foreach (var path in commonPaths)
        {
            if (ValidateGamePath(path))
                return path;
        }
        
        return "";
    }
    
    /// <summary>
    /// Attempts to get game path from Windows registry
    /// </summary>
    /// <returns>The registry path or empty string if not found</returns>
    public static string TryGetGamePathFromRegistry()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";
        
        try
        {
            // Try Bethesda Softworks registry key first
            using var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4");
            if (regKey?.GetValue("installed path") is string bethesdaPath && ValidateGamePath(bethesdaPath))
            {
                return bethesdaPath;
            }
            
            // Try Fallout4VR if regular Fallout4 not found
            using var regKeyVR = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4VR");
            if (regKeyVR?.GetValue("installed path") is string bethesdaVRPath && ValidateGamePath(bethesdaVRPath))
            {
                return bethesdaVRPath;
            }
            
            // Try GOG registry key
            using var regKeyGog = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
            if (regKeyGog?.GetValue("path") is string gogPath && ValidateGamePath(gogPath))
            {
                return gogPath;
            }
        }
        catch
        {
            // Registry access failed, continue to other methods
        }
        
        return "";
    }
    
    /// <summary>
    /// Attempts to get game path from XSE (Script Extender) log files
    /// </summary>
    /// <returns>The XSE log path or empty string if not found</returns>
    public static string TryGetGamePathFromXSELog()
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
            {
                if (File.Exists(logPath))
                {
                    var lines = File.ReadAllLines(logPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("plugin directory"))
                        {
                            // Extract path: "plugin directory = C:\Steam\steamapps\common\Fallout 4\Data\F4SE\Plugins"
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var fullPath = parts[1].Trim();
                                // Remove "\Data\F4SE\Plugins" or "\Data\SKSE\Plugins" suffix to get game root
                                var gamePath = fullPath
                                    .Replace(@"\Data\F4SE\Plugins", "")
                                    .Replace(@"\Data\SKSE\Plugins", "")
                                    .Replace(@"\Data\SKSE64\Plugins", "");
                                    
                                if (ValidateGamePath(gamePath))
                                {
                                    return gamePath;
                                }
                            }
                        }
                    }
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
    /// Validates that a path contains a valid game installation
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <returns>True if the path contains a valid game installation</returns>
    public static bool ValidateGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;
        
        // Check for game executables
        var executables = new[] { "Fallout4.exe", "Fallout4VR.exe", "SkyrimSE.exe", "Skyrim.exe" };
        foreach (var exe in executables)
        {
            if (File.Exists(Path.Combine(path, exe)))
                return true;
        }
        
        return false;
    }
}