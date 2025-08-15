using Scanner111.Core.ModManagers.MO2;

namespace Scanner111.Tests.ModManagers.MO2;

[Collection("ModManager Tests")]
public class MO2ProfileReaderTests : IDisposable
{
    private readonly MO2ProfileReader _reader;
    private readonly string _tempDir;

    public MO2ProfileReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MO2ProfileTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _reader = new MO2ProfileReader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task GetActiveProfileFromIniAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDir, "nonexistent.ini");

        // Act
        var result = await _reader.GetActiveProfileFromIniAsync(nonExistentFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveProfileFromIniAsync_ReturnsProfileName_WhenFound()
    {
        // Arrange
        var iniFile = Path.Combine(_tempDir, "ModOrganizer.ini");
        var iniContent = @"[General]
selected_profile=MyTestProfile
gameName=Fallout 4
[Settings]
something=value";
        await File.WriteAllTextAsync(iniFile, iniContent);

        // Act
        var result = await _reader.GetActiveProfileFromIniAsync(iniFile);

        // Assert
        result.Should().Be("MyTestProfile");
    }

    [Fact]
    public async Task GetActiveProfileFromIniAsync_ReturnsNull_WhenProfileNotSet()
    {
        // Arrange
        var iniFile = Path.Combine(_tempDir, "ModOrganizer.ini");
        var iniContent = @"[General]
gameName=Fallout 4
[Settings]
something=value";
        await File.WriteAllTextAsync(iniFile, iniContent);

        // Act
        var result = await _reader.GetActiveProfileFromIniAsync(iniFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveProfileFromIniAsync_HandlesEmptyProfile()
    {
        // Arrange
        var iniFile = Path.Combine(_tempDir, "ModOrganizer.ini");
        var iniContent = @"[General]
selected_profile=
gameName=Fallout 4";
        await File.WriteAllTextAsync(iniFile, iniContent);

        // Act
        var result = await _reader.GetActiveProfileFromIniAsync(iniFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadProfileSettingsAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var profilePath = Path.Combine(_tempDir, "profile");
        Directory.CreateDirectory(profilePath);

        // Act
        var result = await _reader.ReadProfileSettingsAsync(profilePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadProfileSettingsAsync_ReadsSettings_WhenFileExists()
    {
        // Arrange
        var profilePath = Path.Combine(_tempDir, "profile");
        Directory.CreateDirectory(profilePath);

        var settingsFile = Path.Combine(profilePath, "settings.ini");
        var settingsContent = @"[General]
LocalSavegames=true
AutomaticArchiveInvalidation=false
[Other]
SomeOtherSetting=value";
        await File.WriteAllTextAsync(settingsFile, settingsContent);

        // Act
        var result = await _reader.ReadProfileSettingsAsync(profilePath);

        // Assert
        result.Should().NotBeNull();
        result!.LocalSavegames.Should().BeTrue();
        result.AutomaticArchiveInvalidation.Should().BeFalse();
    }

    [Fact]
    public async Task ReadProfileSettingsAsync_HandlesInvalidValues()
    {
        // Arrange
        var profilePath = Path.Combine(_tempDir, "profile");
        Directory.CreateDirectory(profilePath);

        var settingsFile = Path.Combine(profilePath, "settings.ini");
        var settingsContent = @"[General]
LocalSavegames=invalid
AutomaticArchiveInvalidation=yes";
        await File.WriteAllTextAsync(settingsFile, settingsContent);

        // Act
        var result = await _reader.ReadProfileSettingsAsync(profilePath);

        // Assert
        result.Should().NotBeNull();
        result!.LocalSavegames.Should().BeFalse(); // Default when invalid
        result.AutomaticArchiveInvalidation.Should().BeFalse(); // Default when invalid
    }
}