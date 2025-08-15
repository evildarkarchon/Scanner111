using Scanner111.Core.Models;

namespace Scanner111.Tests.Models;

[Collection("ModManager Tests")]
public class GameConfigurationTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var config = new GameConfiguration();

        config.GameName.Should().Be(string.Empty, "GameName should be empty by default");
        config.RootPath.Should().Be(string.Empty, "RootPath should be empty by default");
        config.ExecutablePath.Should().Be(string.Empty, "ExecutablePath should be empty by default");
        config.DocumentsPath.Should().Be(string.Empty, "DocumentsPath should be empty by default");
        config.FileHashes.Should().NotBeNull("FileHashes dictionary should be initialized");
        config.FileHashes.Should().BeEmpty("FileHashes should be empty by default");
        config.Platform.Should().Be(string.Empty, "Platform should be empty by default");
        config.Version.Should().Be(string.Empty, "Version should be empty by default");
        config.XsePath.Should().Be(string.Empty, "XsePath should be empty by default");
        config.XseVersion.Should().Be(string.Empty, "XseVersion should be empty by default");
        config.ModsPath.Should().Be(string.Empty, "ModsPath should be empty by default");
        config.SteamAppId.Should().Be(string.Empty, "SteamAppId should be empty by default");
        config.RegistryPath.Should().Be(string.Empty, "RegistryPath should be empty by default");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathIsEmpty()
    {
        var config = new GameConfiguration();

        config.IsValid.Should().BeFalse("configuration should be invalid when RootPath is empty");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathIsNull()
    {
        var config = new GameConfiguration
        {
            RootPath = null
        };

        config.IsValid.Should().BeFalse("configuration should be invalid when RootPath is empty");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenRootPathDoesNotExist()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\NonExistent\Path\That\Should\Not\Exist"
        };

        config.IsValid.Should().BeFalse("configuration should be invalid when RootPath is empty");
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenRootPathExists()
    {
        // Use a path that should exist on all Windows systems
        var config = new GameConfiguration
        {
            RootPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        };

        config.IsValid.Should().BeTrue("configuration should be valid when RootPath exists");
    }

    [Fact]
    public void DataPath_ReturnsCorrectPath()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\Games\Fallout4"
        };

        config.DataPath.Should().Be(@"C:\Games\Fallout4\Data", "DataPath should combine RootPath with 'Data'");
    }

    [Fact]
    public void DataPath_HandlesTrailingSlash()
    {
        var config = new GameConfiguration
        {
            RootPath = @"C:\Games\Fallout4\"
        };

        config.DataPath.Should().Be(@"C:\Games\Fallout4\Data", "DataPath should combine RootPath with 'Data'");
    }

    [Fact]
    public void FileHashes_CanBeAddedAndRetrieved()
    {
        var config = new GameConfiguration();

        config.FileHashes["Fallout4.exe"] = "ABC123DEF456";
        config.FileHashes["Fallout4.esm"] = "789XYZ012345";

        config.FileHashes.Should().HaveCount(2, "two file hashes were added");
        config.FileHashes["Fallout4.exe"].Should().Be("ABC123DEF456", "first hash should be stored correctly");
        config.FileHashes["Fallout4.esm"].Should().Be("789XYZ012345", "second hash should be stored correctly");
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

        config.GameName.Should().Be("Fallout 4", "GameName should be set correctly");
        config.RootPath.Should().Be(@"C:\Games\Fallout4", "RootPath should be set correctly");
        config.ExecutablePath.Should().Be(@"C:\Games\Fallout4\Fallout4.exe", "ExecutablePath should be set correctly");
        config.DocumentsPath.Should().Be(@"C:\Users\TestUser\Documents\My Games\Fallout4",
            "DocumentsPath should be set correctly");
        config.Platform.Should().Be("Steam", "Platform should be set correctly");
        config.Version.Should().Be("1.10.163.0", "Version should be set correctly");
        config.XsePath.Should().Be(@"C:\Games\Fallout4\f4se_loader.exe", "XsePath should be set correctly");
        config.XseVersion.Should().Be("0.6.23", "XseVersion should be set correctly");
        config.ModsPath.Should().Be(@"C:\ModOrganizer2\mods", "ModsPath should be set correctly");
        config.SteamAppId.Should().Be("377160", "SteamAppId should be set correctly");
        config.RegistryPath.Should().Be(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4",
            "RegistryPath should be set correctly");
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

        fallout4Config.GameName.Should().NotBe(skyrimConfig.GameName, "game names should be different");
        fallout4Config.RootPath.Should().NotBe(skyrimConfig.RootPath, "root paths should be different");
        fallout4Config.SteamAppId.Should().NotBe(skyrimConfig.SteamAppId, "Steam app IDs should be different");
        fallout4Config.Platform.Should().Be(skyrimConfig.Platform, "both games use Steam platform");
    }

    [Fact]
    public void FileHashes_HandlesEmptyDictionary()
    {
        var config = new GameConfiguration();

        config.FileHashes.Should().NotBeNull("FileHashes dictionary should be initialized");
        config.FileHashes.Should().BeEmpty("FileHashes should be empty initially");
        config.FileHashes.Should().NotContainKey("nonexistent", "empty dictionary should not contain any keys");
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
        config.DataPath.Should().Be(expectedPath, "DataPath should work with forward slashes");
    }

    [Fact]
    public void IsValid_HandlesNetworkPaths()
    {
        var config = new GameConfiguration
        {
            RootPath = @"\\NetworkShare\Games\Fallout4"
        };

        // This will be false unless the network path actually exists
        config.IsValid.Should().BeFalse("configuration should be invalid when RootPath is empty");
    }
}