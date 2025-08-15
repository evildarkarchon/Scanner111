using FluentAssertions;
using Scanner111.Core.ModManagers.MO2;

namespace Scanner111.Tests.ModManagers.MO2;

public class MO2ModListParserTests : IDisposable
{
    private readonly MO2ModListParser _parser;
    private readonly string _tempDir;

    public MO2ModListParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MO2Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _parser = new MO2ModListParser();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ParseModListAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDir, "nonexistent.txt");

        // Act
        var result = await _parser.ParseModListAsync(nonExistentFile, _tempDir);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseModListAsync_ParsesEnabledMods()
    {
        // Arrange
        var modListFile = Path.Combine(_tempDir, "modlist.txt");
        var modListContent = @"# This is a comment
+TestMod1
+TestMod2
TestMod3";
        await File.WriteAllTextAsync(modListFile, modListContent);

        // Create mod folders
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "TestMod1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "TestMod2"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "TestMod3"));

        // Act
        var result = await _parser.ParseModListAsync(modListFile, _tempDir);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(mod => mod.IsEnabled.Should().BeTrue());
        result.Should().Contain(m => m.Name == "TestMod1");
        result.Should().Contain(m => m.Name == "TestMod2");
        result.Should().Contain(m => m.Name == "TestMod3");
    }

    [Fact]
    public async Task ParseModListAsync_ParsesDisabledMods()
    {
        // Arrange
        var modListFile = Path.Combine(_tempDir, "modlist.txt");
        var modListContent = @"-DisabledMod1
+EnabledMod
-DisabledMod2";
        await File.WriteAllTextAsync(modListFile, modListContent);

        // Create mod folders
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "DisabledMod1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "EnabledMod"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "mods", "DisabledMod2"));

        // Act
        var result = await _parser.ParseModListAsync(modListFile, _tempDir);

        // Assert
        result.Should().HaveCount(3);

        var disabledMod1 = result.First(m => m.Name == "DisabledMod1");
        disabledMod1.IsEnabled.Should().BeFalse();

        var enabledMod = result.First(m => m.Name == "EnabledMod");
        enabledMod.IsEnabled.Should().BeTrue();

        var disabledMod2 = result.First(m => m.Name == "DisabledMod2");
        disabledMod2.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ParseModListAsync_ReadsMetaIni_WhenExists()
    {
        // Arrange
        var modListFile = Path.Combine(_tempDir, "modlist.txt");
        var modListContent = "+TestModWithMeta";
        await File.WriteAllTextAsync(modListFile, modListContent);

        var modFolder = Path.Combine(_tempDir, "mods", "TestModWithMeta");
        Directory.CreateDirectory(modFolder);

        var metaIniContent = @"[General]
modName=Test Mod With Metadata
version=1.2.3
author=Test Author
description=This is a test mod
modid=12345
installationFile=TestMod_v1.2.3.zip";

        await File.WriteAllTextAsync(Path.Combine(modFolder, "meta.ini"), metaIniContent);

        // Act
        var result = await _parser.ParseModListAsync(modListFile, _tempDir);

        // Assert
        result.Should().HaveCount(1);
        var mod = result.First();
        mod.Name.Should().Be("Test Mod With Metadata");
        mod.Version.Should().Be("1.2.3");
        mod.Author.Should().Be("Test Author");
        mod.Description.Should().Be("This is a test mod");
        mod.Metadata.Should().ContainKey("NexusModId");
        mod.Metadata["NexusModId"].Should().Be("12345");
    }

    [Fact]
    public async Task ParseModListAsync_AssignsCorrectLoadOrder()
    {
        // Arrange
        var modListFile = Path.Combine(_tempDir, "modlist.txt");
        var modListContent = @"+Mod1
+Mod2
-Mod3
+Mod4";
        await File.WriteAllTextAsync(modListFile, modListContent);

        // Create mod folders
        for (var i = 1; i <= 4; i++) Directory.CreateDirectory(Path.Combine(_tempDir, "mods", $"Mod{i}"));

        // Act
        var result = await _parser.ParseModListAsync(modListFile, _tempDir);

        // Assert
        result.Should().HaveCount(4);
        result.First(m => m.Id == "Mod1").LoadOrder.Should().Be(0);
        result.First(m => m.Id == "Mod2").LoadOrder.Should().Be(1);
        result.First(m => m.Id == "Mod3").LoadOrder.Should().Be(2);
        result.First(m => m.Id == "Mod4").LoadOrder.Should().Be(3);
    }
}