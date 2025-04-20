using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using System.Diagnostics;
using Mutagen.Bethesda.Installs;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;

namespace Scanner111.Infrastructure.Services;

public class GameDetectionService(
    IFileSystemService fileSystemService,
    IPluginSystemService pluginSystemService)
    : IGameDetectionService
{
    public async Task<Game?> DetectGameAsync(string possibleInstallPath)
    {
        // Original implementation remains unchanged
        if (!await fileSystemService.DirectoryExistsAsync(possibleInstallPath))
            return null;
            
        var plugins = await pluginSystemService.DiscoverPluginsAsync();
        
        foreach (var plugin in plugins)
        {
            var game = await plugin.DetectGameAsync(possibleInstallPath);
            if (game != null)
                return game;
        }
        
        return null;
    }
    
    public async Task<IEnumerable<Game>> DetectInstalledGamesAsync()
    {
        var result = new List<Game>();
        
        // First try Mutagen's game detection
        try
        {
            await Task.Run(() => {
                // Try to detect Fallout 4
                if (GameLocations.TryGetGameFolder(GameRelease.Fallout4, out var fo4Path))
                {
                    var game = new Game
                    {
                        Id = "Fallout4",
                        Name = "Fallout 4",
                        ExecutableName = "Fallout4.exe",
                        InstallPath = fo4Path.ToString(),
                        DocumentsPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games", "Fallout4"),
                        Version = "Unknown" // We'll fill this in later
                    };
                    
                    result.Add(game);
                }
                
                // Try to detect Fallout 4 VR
                if (GameLocations.TryGetGameFolder(GameRelease.Fallout4VR, out var fo4VrPath))
                {
                    var game = new Game
                    {
                        Id = "Fallout4VR",
                        Name = "Fallout 4 VR",
                        ExecutableName = "Fallout4VR.exe",
                        InstallPath = fo4VrPath.ToString(),
                        DocumentsPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games", "Fallout4VR"),
                        Version = "Unknown" // We'll fill this in later
                    };
                    
                    result.Add(game);
                }
                
                // Try to detect Skyrim SE
                if (GameLocations.TryGetGameFolder(GameRelease.SkyrimSE, out var skyrimSePath))
                {
                    var game = new Game
                    {
                        Id = "SkyrimSE",
                        Name = "Skyrim Special Edition",
                        ExecutableName = "SkyrimSE.exe",
                        InstallPath = skyrimSePath.ToString(),
                        DocumentsPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games", "Skyrim Special Edition"),
                        Version = "Unknown" // We'll fill this in later
                    };
                    
                    result.Add(game);
                }
            });
            
            // Get versions for all detected games
            foreach (var game in result)
            {
                var exePath = Path.Combine(game.InstallPath, game.ExecutableName);
                if (await fileSystemService.FileExistsAsync(exePath))
                {
                    game.Version = await GetGameVersionAsync(exePath) ?? "Unknown";
                }
                
                // Verify documents path exists, otherwise try to find it
                if (!await fileSystemService.DirectoryExistsAsync(game.DocumentsPath))
                {
                    game.DocumentsPath = await FindGameDocumentsPathAsync(game) ?? string.Empty;
                }
            }
            
            // If we found any games with Mutagen, return them
            if (result.Count > 0)
                return result;
        }
        catch (Exception ex)
        {
            // If Mutagen fails, log the exception and fall back to the original method
            Console.WriteLine($"Mutagen detection failed: {ex.Message}");
        }
        
        // Original game detection method as fallback - implementation remains unchanged
        var plugins = await pluginSystemService.DiscoverPluginsAsync();
        
        // Common Steam installation paths
        var possiblePaths = new List<string>
        {
            @"C:\Program Files (x86)\Steam\steamapps\common",
            @"C:\Program Files\Steam\steamapps\common",
            @"D:\Steam\steamapps\common",
            @"E:\Steam\steamapps\common"
        };
        
        // Common GOG installation paths
        possiblePaths.AddRange([
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files\GOG Galaxy\Games",
            @"D:\GOG Galaxy\Games",
            @"E:\GOG Galaxy\Games"
        ]);
        
        // Look in all possible paths
        foreach (var basePath in possiblePaths)
        {
            if (!await fileSystemService.DirectoryExistsAsync(basePath))
                continue;
                
            var gameDirs = await fileSystemService.GetDirectoriesAsync(basePath, "*");
            
            foreach (var gameDir in gameDirs)
            {
                var game = await DetectGameAsync(gameDir);
                if (game != null && result.All(g => g.Id != game.Id))
                {
                    // Make sure we have the documents path
                    if (string.IsNullOrEmpty(game.DocumentsPath))
                    {
                        game.DocumentsPath = await FindGameDocumentsPathAsync(game) ?? string.Empty;
                    }
                    
                    result.Add(game);
                }
            }
        }
        
        return result;
    }
    
    public async Task<string?> FindGameDocumentsPathAsync(Game game)
    {
        // Check if game already has a documents path
        if (!string.IsNullOrEmpty(game.DocumentsPath) && 
            await fileSystemService.DirectoryExistsAsync(game.DocumentsPath))
            return game.DocumentsPath;
            
        // Try to find the documents path based on the game
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var possiblePaths = new List<string>();
        
        switch (game.Id)
        {
            case "Fallout4":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Fallout4"));
                break;
            case "Fallout4VR":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Fallout4VR"));
                break;
            case "SkyrimSE":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Skyrim Special Edition"));
                // GOG version might use a different path
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Skyrim Special Edition GOG"));
                break;
            default:
                return null;
        }
        
        foreach (var path in possiblePaths)
        {
            if (await fileSystemService.DirectoryExistsAsync(path))
                return path;
        }
        
        return null;
    }
    
    public async Task<string?> FindGameInstallPathAsync(string gameName)
    {
        // Try Mutagen first
        try
        {
            // Only try to use Mutagen for the games we know it supports
            bool isMutagenSupported = true;
                GameRelease gameRelease = gameName switch
                {
                    "Fallout4" => GameRelease.Fallout4,
                    "Fallout4VR" => GameRelease.Fallout4VR,
                    "SkyrimSE" => GameRelease.SkyrimSE,
                    _ => (isMutagenSupported = false, GameRelease.Fallout4).Item2 // Use tuple to set flag and return value
                };
        
            if (isMutagenSupported && 
                GameLocations.TryGetGameFolder(gameRelease, out var gamePath))
            {
                return gamePath.ToString();
            }
        }
        catch
        {
            // Fall back to the standard method
        }
        
        // Original implementation remains as fallback
        // Common Steam installation paths
        var possiblePaths = new List<string>
        {
            @"C:\Program Files (x86)\Steam\steamapps\common",
            @"C:\Program Files\Steam\steamapps\common",
            @"D:\Steam\steamapps\common",
            @"E:\Steam\steamapps\common"
        };
        
        // Common GOG installation paths
        possiblePaths.AddRange([
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files\GOG Galaxy\Games",
            @"D:\GOG Galaxy\Games",
            @"E:\GOG Galaxy\Games"
        ]);
        
        // Define common folder names for each game
        var folderNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        folderNames["Fallout4"] = ["Fallout 4"];
        folderNames["Fallout4VR"] = ["Fallout 4 VR"];
        folderNames["SkyrimSE"] = ["Skyrim Special Edition"];
        
        if (!folderNames.TryGetValue(gameName, out var gamefolderNames))
            return null;
            
        foreach (var basePath in possiblePaths)
        {
            if (!await fileSystemService.DirectoryExistsAsync(basePath))
                continue;
                
            foreach (var folderName in gamefolderNames)
            {
                var fullPath = Path.Combine(basePath, folderName);
                if (await fileSystemService.DirectoryExistsAsync(fullPath))
                    return fullPath;
            }
        }
        
        return null;
    }
    
    public async Task<string?> GetGameVersionAsync(string exePath)
    {
        if (!await fileSystemService.FileExistsAsync(exePath))
            return null;
            
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return versionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }
}