using System.Security.Cryptography;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides functionality to detect and validate game versions
/// </summary>
public class GameVersionDetection
{
    // Known Fallout 4 versions with their SHA256 hashes
    private static readonly Dictionary<string, GameVersionInfo> KnownFallout4Versions = new()
    {
        {
            "3c3e4d89f88d28d2674ea9c968dfa14f6c1461cdcc69c833bb3c96f46329e76b", // Hash to be verified
            new GameVersionInfo
            {
                Version = "1.10.163.0",
                Name = "Pre-Next Gen Update",
                ReleaseDate = new DateTime(2019, 4, 28),
                Description = "Most stable version for modding - highest mod compatibility",
                RequiredF4seVersion = "0.6.23",
                Notes = new[]
                {
                    "Most mods are built for this version",
                    "Best stability with large mod lists",
                    "Recommended for heavy modding"
                }
            }
        },
        {
            "8b3c1c3f3e3d28d2674ea9c968dfa14f6c1461cdcc69c833bb3c96f46329e99a", // Hash to be verified
            new GameVersionInfo
            {
                Version = "1.10.984.0",
                Name = "Next Gen Update",
                ReleaseDate = new DateTime(2024, 4, 25),
                Description = "Latest version with graphical improvements",
                RequiredF4seVersion = "0.7.2",
                Notes = new[]
                {
                    "Many mods require updates for this version",
                    "Improved performance and graphics",
                    "Some older mods may not be compatible",
                    "Check mod compatibility before updating"
                }
            }
        }
    };
    
    /// <summary>
    /// Detects the game version by calculating the executable's hash
    /// </summary>
    public static async Task<GameVersionInfo?> DetectGameVersionAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath))
            return null;
            
        try
        {
            var hash = await CalculateFileHashAsync(executablePath, cancellationToken).ConfigureAwait(false);
            
            if (KnownFallout4Versions.TryGetValue(hash, out var versionInfo))
            {
                return versionInfo;
            }
            
            // Unknown version
            return new GameVersionInfo
            {
                Version = "Unknown",
                Name = "Unknown Version",
                Description = "Unrecognized game version - may be pirated or modified",
                ExecutableHash = hash,
                Notes = new[]
                {
                    "Version not recognized - could be:",
                    "- Pirated or cracked version",
                    "- Beta or pre-release version",
                    "- Modified executable",
                    "Mod compatibility cannot be guaranteed"
                }
            };
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Calculates SHA256 hash of a file
    /// </summary>
    public static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            // Re-throw as TaskCanceledException for consistency
            throw new TaskCanceledException();
        }
    }
    
    /// <summary>
    /// Validates if a specific version is compatible with a given F4SE version
    /// </summary>
    public static bool IsF4seCompatible(string gameVersion, string f4seVersion)
    {
        var versionInfo = KnownFallout4Versions.Values.FirstOrDefault(v => v.Version == gameVersion);
        if (versionInfo == null)
            return false;
            
        // Simple version comparison - in reality would need more complex logic
        return string.CompareOrdinal(f4seVersion, versionInfo.RequiredF4seVersion) >= 0;
    }
    
    /// <summary>
    /// Gets version-specific compatibility notes
    /// </summary>
    public static string[] GetVersionCompatibilityNotes(string gameVersion)
    {
        var versionInfo = KnownFallout4Versions.Values.FirstOrDefault(v => v.Version == gameVersion);
        return versionInfo?.Notes ?? new[] { "Unknown version - compatibility cannot be determined" };
    }
}

/// <summary>
/// Represents information about a specific game version
/// </summary>
public class GameVersionInfo
{
    /// <summary>
    /// Version number (e.g., "1.10.163.0")
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Friendly name for the version
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Release date of this version
    /// </summary>
    public DateTime? ReleaseDate { get; set; }
    
    /// <summary>
    /// Description of this version
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of the executable
    /// </summary>
    public string ExecutableHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Required F4SE/SKSE version for this game version
    /// </summary>
    public string RequiredF4seVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Compatibility notes and warnings
    /// </summary>
    public string[] Notes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether this is a known good version
    /// </summary>
    public bool IsKnownVersion => Version != "Unknown";
    
    /// <summary>
    /// Whether this version is recommended for modding
    /// </summary>
    public bool IsModdingRecommended => Version == "1.10.163.0";
}