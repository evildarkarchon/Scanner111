using FluentAssertions;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for the ScanGameSections static class.
/// </summary>
public class ScanGameSectionsTests
{
    #region Header Tests

    [Fact]
    public void CreateMainHeader_ReturnsCorrectFormat()
    {
        // Arrange
        var gameName = "Fallout 4";
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var fragment = ScanGameSections.CreateMainHeader(gameName, timestamp);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Fallout 4");
        content.Should().Contain("2024-01-15");
    }

    [Fact]
    public void CreateUnpackedSectionHeader_ReturnsHeader()
    {
        // Act
        var fragment = ScanGameSections.CreateUnpackedSectionHeader();

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Unpacked");
        content.Should().Contain("Loose");
    }

    [Fact]
    public void CreateArchivedSectionHeader_ReturnsHeader()
    {
        // Act
        var fragment = ScanGameSections.CreateArchivedSectionHeader();

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Archived");
        content.Should().Contain("BA2");
    }

    #endregion

    #region Texture Issue Tests

    [Fact]
    public void CreateUnpackedTextureDimensionIssues_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var issues = new List<UnpackedTextureDimensionIssue>();

        // Act
        var fragment = ScanGameSections.CreateUnpackedTextureDimensionIssues(issues);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void CreateUnpackedTextureDimensionIssues_WithIssues_IncludesHeaderAndItems()
    {
        // Arrange
        var issues = new List<UnpackedTextureDimensionIssue>
        {
            new("C:\\Mods\\ModA\\texture.dds", "ModA\\texture.dds", 255, 256, "Width not divisible by 2")
        };

        // Act
        var fragment = ScanGameSections.CreateUnpackedTextureDimensionIssues(issues);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("DDS DIMENSIONS");
        content.Should().Contain("255x256");
        content.Should().Contain("ModA\\texture.dds");
    }

    [Fact]
    public void CreateUnpackedTextureDimensionIssues_SortsItemsByPath()
    {
        // Arrange
        var issues = new List<UnpackedTextureDimensionIssue>
        {
            new("C:\\Mods\\ZMod\\tex.dds", "ZMod\\tex.dds", 255, 256, "Issue"),
            new("C:\\Mods\\AMod\\tex.dds", "AMod\\tex.dds", 255, 256, "Issue"),
            new("C:\\Mods\\MMod\\tex.dds", "MMod\\tex.dds", 255, 256, "Issue")
        };

        // Act
        var fragment = ScanGameSections.CreateUnpackedTextureDimensionIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        var aIndex = content.IndexOf("AMod");
        var mIndex = content.IndexOf("MMod");
        var zIndex = content.IndexOf("ZMod");

        aIndex.Should().BeLessThan(mIndex);
        mIndex.Should().BeLessThan(zIndex);
    }

    [Fact]
    public void CreateUnpackedTextureFormatIssues_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var issues = new List<UnpackedTextureFormatIssue>
        {
            new("C:\\Mods\\ModA\\texture.tga", "ModA\\texture.tga", "tga"),
            new("C:\\Mods\\ModB\\texture.png", "ModB\\texture.png", "png")
        };

        // Act
        var fragment = ScanGameSections.CreateUnpackedTextureFormatIssues(issues);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("SHOULD BE DDS");
        content.Should().Contain("TGA");
        content.Should().Contain("PNG");
    }

    #endregion

    #region Sound Issue Tests

    [Fact]
    public void CreateUnpackedSoundFormatIssues_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var issues = new List<UnpackedSoundFormatIssue>
        {
            new("C:\\Mods\\ModA\\sound.mp3", "ModA\\sound.mp3", "mp3")
        };

        // Act
        var fragment = ScanGameSections.CreateUnpackedSoundFormatIssues(issues);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("SHOULD BE XWM OR WAV");
        content.Should().Contain("MP3");
    }

    [Fact]
    public void CreateArchivedSoundFormatIssues_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var issues = new List<SoundFormatIssue>();

        // Act
        var fragment = ScanGameSections.CreateArchivedSoundFormatIssues(issues);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    #endregion

    #region XSE File Issue Tests

    [Fact]
    public void CreateUnpackedXseFileIssues_WithIssues_UsesCorrectAcronym()
    {
        // Arrange
        var issues = new List<UnpackedXseFileIssue>
        {
            new("C:\\Mods\\ModA\\Scripts", "ModA\\Scripts")
        };

        // Act
        var fragment = ScanGameSections.CreateUnpackedXseFileIssues(issues, "F4SE");

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("F4SE");
        content.Should().Contain("FOLDERS CONTAIN");
    }

    [Fact]
    public void CreateArchivedXseFileIssues_WithIssues_UsesCorrectAcronym()
    {
        // Arrange
        var issues = new List<XseFileIssue>
        {
            new("ModA.ba2", "scripts/actor.pex")
        };

        // Act
        var fragment = ScanGameSections.CreateArchivedXseFileIssues(issues, "SKSE64");

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("SKSE64");
        content.Should().Contain("BA2 ARCHIVES CONTAIN");
    }

    #endregion

    #region Unpacked-Specific Issue Tests

    [Fact]
    public void CreatePrevisFileIssues_WithIssues_IncludesPRPInfo()
    {
        // Arrange
        var issues = new List<PrevisFileIssue>
        {
            new("C:\\Mods\\ModA\\Vis", "ModA\\Vis")
        };

        // Act
        var fragment = ScanGameSections.CreatePrevisFileIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("PRECOMBINE");
        content.Should().Contain("PRP");
    }

    [Fact]
    public void CreateAnimationDataIssues_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var issues = new List<AnimationDataIssue>
        {
            new("C:\\Mods\\ModA\\AnimationFileData", "ModA\\AnimationFileData")
        };

        // Act
        var fragment = ScanGameSections.CreateAnimationDataIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("ANIMATION FILE DATA");
    }

    [Fact]
    public void CreateCleanupIssues_WithMixedTypes_FormatsCorrectly()
    {
        // Arrange
        var issues = new List<CleanupIssue>
        {
            new("C:\\Mods\\ModA\\readme.txt", "ModA\\readme.txt", CleanupItemType.ReadmeFile),
            new("C:\\Mods\\ModB\\fomod", "ModB\\fomod", CleanupItemType.FomodFolder)
        };

        // Act
        var fragment = ScanGameSections.CreateCleanupIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("DOCUMENTATION FILES");
        content.Should().Contain("readme file");
        content.Should().Contain("FOMOD folder");
    }

    #endregion

    #region BA2-Specific Issue Tests

    [Fact]
    public void CreateBA2FormatIssues_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var issues = new List<BA2FormatIssue>
        {
            new("C:\\Mods\\ModA.ba2", "ModA.ba2", "BAAD")
        };

        // Act
        var fragment = ScanGameSections.CreateBA2FormatIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("BTDX-GNRL OR BTDX-DX10");
        content.Should().Contain("BAAD");
    }

    #endregion

    #region Configuration Issue Tests

    [Fact]
    public void CreateConfigIssues_GroupsBySeverity()
    {
        // Arrange
        var issues = new List<ConfigIssue>
        {
            new("C:\\path", "fallout4.ini", "General", "sIntroSequence", "1", "0", "Disable intro", ConfigIssueSeverity.Info),
            new("C:\\path", "fallout4.ini", "Display", "bUseTAA", "0", "1", "Enable TAA", ConfigIssueSeverity.Warning),
            new("C:\\path", "fallout4.ini", "Archive", "bInvalidateOlderFiles", "0", "1", "Required for mods", ConfigIssueSeverity.Error)
        };

        // Act
        var fragment = ScanGameSections.CreateConfigIssues(issues);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Errors:");
        content.Should().Contain("Warnings:");
        content.Should().Contain("Info:");
    }

    [Fact]
    public void CreateTomlIssues_WithMissingConfig_ShowsWarning()
    {
        // Arrange
        var result = new TomlScanResult
        {
            CrashGenName = "Buffout4",
            ConfigFileFound = false
        };

        // Act
        var fragment = ScanGameSections.CreateTomlIssues(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("BUFFOUT4");
        content.Should().Contain("not found");
    }

    #endregion

    #region XSE Status Issue Tests

    [Fact]
    public void CreateXseStatusIssues_WithMissingAddressLibrary_ReportsIssue()
    {
        // Arrange
        var result = new XseScanResult
        {
            AddressLibraryInstalled = false,
            AddressLibraryStatus = AddressLibraryStatus.Missing,
            XseInstalled = true,
            IsLatestVersion = true
        };

        // Act
        var fragment = ScanGameSections.CreateXseStatusIssues(result, "F4SE");

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Address Library");
        content.Should().Contain("NOT installed");
    }

    [Fact]
    public void CreateXseStatusIssues_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new XseScanResult
        {
            AddressLibraryInstalled = true,
            XseInstalled = true,
            IsLatestVersion = true
        };

        // Act
        var fragment = ScanGameSections.CreateXseStatusIssues(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    #endregion

    #region Game Integrity Issue Tests

    [Fact]
    public void CreateGameIntegrityIssues_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var result = new GameIntegrityResult
        {
            VersionStatus = ExecutableVersionStatus.Outdated,
            Issues = new List<GameIntegrityIssue>
            {
                new(GameIntegrityIssueType.OutdatedVersion, "Game is outdated", ConfigIssueSeverity.Warning, "Update the game")
            }
        };

        // Act
        var fragment = ScanGameSections.CreateGameIntegrityIssues(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("GAME INSTALLATION");
        content.Should().Contain("outdated");
    }

    [Fact]
    public void CreateGameIntegrityIssues_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new GameIntegrityResult
        {
            VersionStatus = ExecutableVersionStatus.LatestVersion,
            LocationStatus = InstallationLocationStatus.RecommendedLocation
        };

        // Act
        var fragment = ScanGameSections.CreateGameIntegrityIssues(result);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    #endregion

    #region Footer and Success Tests

    [Fact]
    public void CreateFooter_ReturnsScanner111Credit()
    {
        // Act
        var fragment = ScanGameSections.CreateFooter();

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Scanner111");
    }

    [Fact]
    public void CreateNoIssuesFound_ReturnsSuccessMessage()
    {
        // Act
        var fragment = ScanGameSections.CreateNoIssuesFound();

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("No issues found");
        content.Should().Contain("✅");
    }

    #endregion
}
