using System;
using System.IO;
using System.Linq;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Manages crash log directory structure with game-specific subfolders
/// </summary>
public static class CrashLogDirectoryManager
{
    /// <summary>
    /// Gets the default crash logs directory path
    /// </summary>
    public static string GetDefaultCrashLogsDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                           "Scanner111", "Crash Logs");
    }
    
    /// <summary>
    /// Detects the game type from a game path or crash log content
    /// </summary>
    /// <param name="gamePath">Optional game installation path</param>
    /// <param name="crashLogPath">Optional crash log file to analyze</param>
    /// <returns>Game subfolder name</returns>
    public static string DetectGameType(string? gamePath = null, string? crashLogPath = null)
    {
        // Try to detect from game path first
        if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
        {
            var executableFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToArray();
                
            if (executableFiles.Any(f => f?.Equals("Fallout4.exe", StringComparison.OrdinalIgnoreCase) == true))
                return "Fallout4";
            if (executableFiles.Any(f => f?.Equals("Fallout4VR.exe", StringComparison.OrdinalIgnoreCase) == true))
                return "Fallout4VR";
            if (executableFiles.Any(f => f?.Equals("SkyrimSE.exe", StringComparison.OrdinalIgnoreCase) == true))
                return "SkyrimSE";
            if (executableFiles.Any(f => f?.Equals("Skyrim.exe", StringComparison.OrdinalIgnoreCase) == true))
                return "Skyrim";
        }
        
        // Try to detect from crash log path and content
        if (!string.IsNullOrEmpty(crashLogPath))
        {
            try
            {
                // First check the file path for XSE directory hints (even if file doesn't exist)
                if (crashLogPath.Contains("\\F4SE\\") || crashLogPath.Contains("/F4SE/"))
                {
                    if (crashLogPath.Contains("Fallout4VR", StringComparison.OrdinalIgnoreCase))
                        return "Fallout4VR";
                    else
                        return "Fallout4";
                }
                if (crashLogPath.Contains("\\SKSE\\") || crashLogPath.Contains("/SKSE/"))
                {
                    // Check for Skyrim Special Edition (standard and GOG versions)
                    if (crashLogPath.Contains("Skyrim Special Edition", StringComparison.OrdinalIgnoreCase))
                        return "SkyrimSE";
                }
                
                // Then check file content if file exists
                if (File.Exists(crashLogPath))
                {
                    var lines = File.ReadLines(crashLogPath).Take(20).ToArray();
                    foreach (var line in lines)
                    {
                        var lowerLine = line.ToLowerInvariant();
                        if (lowerLine.Contains("fallout4.exe") || lowerLine.Contains("fallout 4"))
                            return "Fallout4";
                        if (lowerLine.Contains("fallout4vr.exe") || lowerLine.Contains("fallout 4 vr"))
                            return "Fallout4VR";
                        if (lowerLine.Contains("skyrimse.exe") || lowerLine.Contains("skyrim special edition"))
                            return "SkyrimSE";
                    }
                }
            }
            catch
            {
                // Ignore errors reading crash log for detection
            }
        }
        
        // Default fallback - assume Fallout 4 as it's the most common
        return "Fallout4";
    }
    
    /// <summary>
    /// Gets the full path for a game-specific crash logs directory
    /// </summary>
    /// <param name="baseDirectory">Base crash logs directory</param>
    /// <param name="gameType">Game type (e.g., "Fallout4", "SkyrimSE")</param>
    /// <returns>Full path to game-specific directory</returns>
    public static string GetGameSpecificDirectory(string baseDirectory, string gameType)
    {
        return Path.Combine(baseDirectory, gameType);
    }
    
    /// <summary>
    /// Ensures the crash logs directory structure exists
    /// </summary>
    /// <param name="baseDirectory">Base crash logs directory</param>
    /// <param name="gameType">Game type to create subfolder for</param>
    /// <returns>Path to the game-specific directory</returns>
    public static string EnsureDirectoryExists(string baseDirectory, string gameType)
    {
        var gameDirectory = GetGameSpecificDirectory(baseDirectory, gameType);
        
        try
        {
            Directory.CreateDirectory(gameDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create crash logs directory: {gameDirectory}", ex);
        }
        
        return gameDirectory;
    }
    
    /// <summary>
    /// Gets the target path for copying a crash log file
    /// </summary>
    /// <param name="baseDirectory">Base crash logs directory</param>
    /// <param name="gameType">Game type</param>
    /// <param name="originalFilePath">Original crash log file path</param>
    /// <returns>Target path for the copied file</returns>
    public static string GetTargetPath(string baseDirectory, string gameType, string originalFilePath)
    {
        var gameDirectory = EnsureDirectoryExists(baseDirectory, gameType);
        var fileName = Path.GetFileName(originalFilePath);
        return Path.Combine(gameDirectory, fileName);
    }
    
    /// <summary>
    /// Copies a crash log file to the appropriate game-specific directory
    /// </summary>
    /// <param name="sourceFilePath">Source crash log file</param>
    /// <param name="baseDirectory">Base crash logs directory</param>
    /// <param name="gameType">Game type (optional - will auto-detect if not provided)</param>
    /// <param name="overwrite">Whether to overwrite existing files</param>
    /// <returns>Path to the copied file</returns>
    public static string CopyCrashLog(string sourceFilePath, string baseDirectory, string? gameType = null, bool overwrite = true)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source crash log file not found: {sourceFilePath}");
            
        // Auto-detect game type if not provided
        if (string.IsNullOrEmpty(gameType))
        {
            gameType = DetectGameType(crashLogPath: sourceFilePath);
        }
        
        var targetPath = GetTargetPath(baseDirectory, gameType, sourceFilePath);
        
        // Only copy if target doesn't exist or source is newer
        if (!File.Exists(targetPath) || overwrite)
        {
            var sourceInfo = new FileInfo(sourceFilePath);
            var targetInfo = File.Exists(targetPath) ? new FileInfo(targetPath) : null;
            
            if (targetInfo == null || sourceInfo.LastWriteTime > targetInfo.LastWriteTime)
            {
                File.Copy(sourceFilePath, targetPath, overwrite);
            }
        }
        
        return targetPath;
    }
}