using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Models;

public class GameConfigurationTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var config = new GameConfiguration();
        
        Assert.Equal(string.Empty, config.GameName);
        Assert.Equal(string.Empty, config.RootPath);
        Assert.Equal(string.Empty, config.ExecutablePath);
        Assert.Equal(string.Empty, config.DocumentsPath);
        Assert.NotNull(config.FileHashes);
        Assert.Empty(config.FileHashes);
        Assert.Equal(string.Empty, config.Platform);
        Assert.Equal(string.Empty, config.Version);
        Assert.Equal(string.Empty, config.XsePath);
        Assert.Equal(string.Empty, config.XseVersion);
        Assert.Equal(string.Empty, config.ModsPath);
        Assert.Equal(string.Empty, config.SteamAppId);
        Assert.Equal(string.Empty, config.RegistryPath);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathIsEmpty()
    {
        var config = new GameConfiguration();
        
        Assert.False(config.IsValid);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathIsNull()
    {
        var config = new GameConfiguration
        {
            RootPath = null
        };
        
        Assert.False(config.IsValid);
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathDoesNotExist()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\NonExistent\Path\That\Should\Not\Exist"
        };
        
        Assert.False(config.IsValid);
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenRootPathExists()
    {
        // Use a path that should exist on all Windows systems
        var config = new GameConfiguration
        {
            RootPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        };
        
        Assert.True(config.IsValid);
    }

    [Fact]
    public void DataPath_ReturnsCorrectPath()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\Games\Fallout4"
        };
        
        Assert.Equal(@"C:\Games\Fallout4\Data", config.DataPath);
    }

    [Fact]
    public void DataPath_HandlesTrailingSlash()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\Games\Fallout4\"
        };
        
        Assert.Equal(@"C:\Games\Fallout4\Data", config.DataPath);
    }

    [Fact]
    public void FileHashes_CanBeAddedAndRetrieved()
    {
        var config = new GameConfiguration();
        
        config.FileHashes["Fallout4.exe"] = "ABC123DEF456";
        config.FileHashes["Fallout4.esm"] = "789XYZ012345";
        
        Assert.Equal(2, config.FileHashes.Count);
        Assert.Equal("ABC123DEF456", config.FileHashes["Fallout4.exe"]);
        Assert.Equal("789XYZ012345", config.FileHashes["Fallout4.esm"]);
    }

    [Fact]
    public void AllPropertiesCanBeSetAndRetrieved()
    {
        var config = new GameConfiguration
        {
            GameName = "Fallout 4",
            RootPath = @"C:\Games\Fallout4",
            ExecutablePath = @"C:\Games\Fallout4\Fallout4.exe",
            DocumentsPath = @"C:\Users\TestUser\Documents\My Games\Fallout4",
            Platform = "Steam",
            Version = "1.10.163.0",
            XsePath = @"C:\Games\Fallout4\f4se_loader.exe",
            XseVersion = "0.6.23",
            ModsPath = @"C:\ModOrganizer2\mods",
            SteamAppId = "377160",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4"
        };
        
        Assert.Equal("Fallout 4", config.GameName);
        Assert.Equal(@"C:\Games\Fallout4", config.RootPath);
        Assert.Equal(@"C:\Games\Fallout4\Fallout4.exe", config.ExecutablePath);
        Assert.Equal(@"C:\Users\TestUser\Documents\My Games\Fallout4", config.DocumentsPath);
        Assert.Equal("Steam", config.Platform);
        Assert.Equal("1.10.163.0", config.Version);
        Assert.Equal(@"C:\Games\Fallout4\f4se_loader.exe", config.XsePath);
        Assert.Equal("0.6.23", config.XseVersion);
        Assert.Equal(@"C:\ModOrganizer2\mods", config.ModsPath);
        Assert.Equal("377160", config.SteamAppId);
        Assert.Equal(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4", config.RegistryPath);
    }

    [Fact]
    public void GameConfiguration_SupportsMultipleGames()
    {
        var fallout4Config = new GameConfiguration
        {
            GameName = "Fallout 4",
            RootPath = @"C:\Games\Fallout4",
            Platform = "Steam",
            SteamAppId = "377160"
        };
        
        var skyrimConfig = new GameConfiguration
        {
            GameName = "Skyrim Special Edition",
            RootPath = @"C:\Games\SkyrimSE",
            Platform = "Steam",
            SteamAppId = "489830"
        };
        
        Assert.NotEqual(fallout4Config.GameName, skyrimConfig.GameName);
        Assert.NotEqual(fallout4Config.RootPath, skyrimConfig.RootPath);
        Assert.NotEqual(fallout4Config.SteamAppId, skyrimConfig.SteamAppId);
        Assert.Equal(fallout4Config.Platform, skyrimConfig.Platform);
    }

    [Fact]
    public void FileHashes_HandlesEmptyDictionary()
    {
        var config = new GameConfiguration();
        
        Assert.NotNull(config.FileHashes);
        Assert.Empty(config.FileHashes);
        Assert.False(config.FileHashes.ContainsKey("nonexistent"));
    }

    [Fact]
    public void DataPath_WorksWithForwardSlashes()
    {
        var config = new GameConfiguration
        {
            RootPath = "C:/Games/Fallout4"
        };
        
        // Path.Combine should handle this correctly on Windows
        var expectedPath = Path.Combine("C:/Games/Fallout4", "Data");
        Assert.Equal(expectedPath, config.DataPath);
    }

    [Fact]
    public void IsValid_HandlesNetworkPaths()
    {
        var config = new GameConfiguration
        {
            RootPath = @"\\NetworkShare\Games\Fallout4"
        };
        
        // This will be false unless the network path actually exists
        Assert.False(config.IsValid);
    }
}