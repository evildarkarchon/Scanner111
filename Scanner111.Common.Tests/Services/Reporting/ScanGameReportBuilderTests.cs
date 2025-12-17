using FluentAssertions;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for the ScanGameReportBuilder class.
/// </summary>
public class ScanGameReportBuilderTests
{
    private readonly ScanGameReportBuilder _builder;

    public ScanGameReportBuilderTests()
    {
        _builder = new ScanGameReportBuilder();
    }

    #region Unpacked Section Tests

    [Fact]
    public void BuildUnpackedSection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new UnpackedScanResult();

        // Act
        var fragment = _builder.BuildUnpackedSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildUnpackedSection_WithIssues_IncludesAllCategories()
    {
        // Arrange
        var result = new UnpackedScanResult
        {
            CleanupIssues = new List<CleanupIssue>
            {
                new("C:\\readme.txt", "readme.txt", CleanupItemType.ReadmeFile)
            },
            TextureFormatIssues = new List<UnpackedTextureFormatIssue>
            {
                new("C:\\tex.tga", "tex.tga", "tga")
            },
            TextureDimensionIssues = new List<UnpackedTextureDimensionIssue>
            {
                new("C:\\tex.dds", "tex.dds", 255, 256, "Width issue")
            }
        };

        // Act
        var fragment = _builder.BuildUnpackedSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Unpacked");
        content.Should().Contain("DOCUMENTATION FILES");
        content.Should().Contain("TEXTURE FILES HAVE INCORRECT FORMAT");
        content.Should().Contain("DDS DIMENSIONS");
    }

    #endregion

    #region Archived Section Tests

    [Fact]
    public void BuildArchivedSection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new BA2ScanResult();

        // Act
        var fragment = _builder.BuildArchivedSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildArchivedSection_WithBA2FormatIssues_IncludesHeader()
    {
        // Arrange
        var result = new BA2ScanResult
        {
            FormatIssues = new List<BA2FormatIssue>
            {
                new("C:\\bad.ba2", "bad.ba2", "BAAD")
            }
        };

        // Act
        var fragment = _builder.BuildArchivedSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Archived");
        content.Should().Contain("BA2");
        content.Should().Contain("INCORRECT FORMAT");
    }

    #endregion

    #region INI Section Tests

    [Fact]
    public void BuildIniSection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new IniScanResult();

        // Act
        var fragment = _builder.BuildIniSection(result);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildIniSection_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var result = new IniScanResult
        {
            ConfigIssues = new List<ConfigIssue>
            {
                new("C:\\fallout4.ini", "fallout4.ini", "Archive", "bInvalidateOlderFiles", "0", "1", "Required", ConfigIssueSeverity.Error)
            }
        };

        // Act
        var fragment = _builder.BuildIniSection(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("INI CONFIGURATION");
        content.Should().Contain("bInvalidateOlderFiles");
    }

    #endregion

    #region TOML Section Tests

    [Fact]
    public void BuildTomlSection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new TomlScanResult
        {
            ConfigFileFound = true,
            HasDuplicateConfigs = false,
            ConfigIssues = Array.Empty<ConfigIssue>()
        };

        // Act
        var fragment = _builder.BuildTomlSection(result);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildTomlSection_WithMissingConfig_ReportsIssue()
    {
        // Arrange
        var result = new TomlScanResult
        {
            CrashGenName = "Buffout4",
            ConfigFileFound = false
        };

        // Act
        var fragment = _builder.BuildTomlSection(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Buffout4");
    }

    #endregion

    #region XSE Section Tests

    [Fact]
    public void BuildXseSection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new XseScanResult
        {
            AddressLibraryInstalled = true,
            XseInstalled = true,
            IsLatestVersion = true
        };

        // Act
        var fragment = _builder.BuildXseSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildXseSection_WithOutdatedVersion_ReportsIssue()
    {
        // Arrange
        var result = new XseScanResult
        {
            AddressLibraryInstalled = true,
            XseInstalled = true,
            IsLatestVersion = false,
            DetectedVersion = "0.6.20"
        };

        // Act
        var fragment = _builder.BuildXseSection(result, "F4SE");

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("F4SE");
        content.Should().Contain("0.6.20");
        content.Should().Contain("outdated");
    }

    #endregion

    #region Integrity Section Tests

    [Fact]
    public void BuildIntegritySection_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new GameIntegrityResult
        {
            VersionStatus = ExecutableVersionStatus.LatestVersion,
            LocationStatus = InstallationLocationStatus.RecommendedLocation
        };

        // Act
        var fragment = _builder.BuildIntegritySection(result);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void BuildIntegritySection_WithIssues_FormatsCorrectly()
    {
        // Arrange
        var result = new GameIntegrityResult
        {
            VersionStatus = ExecutableVersionStatus.Outdated,
            Issues = new List<GameIntegrityIssue>
            {
                new(GameIntegrityIssueType.OutdatedVersion, "Outdated", ConfigIssueSeverity.Warning, "Update")
            }
        };

        // Act
        var fragment = _builder.BuildIntegritySection(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("GAME INSTALLATION");
    }

    #endregion

    #region Combined Report Tests

    [Fact]
    public void BuildCombinedReport_WithAllResults_ComposesInOrder()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Fallout 4",
            XseAcronym = "F4SE",
            ScanTimestamp = DateTimeOffset.Now,
            UnpackedResult = new UnpackedScanResult
            {
                CleanupIssues = new List<CleanupIssue>
                {
                    new("C:\\readme.txt", "readme.txt", CleanupItemType.ReadmeFile)
                }
            },
            ArchivedResult = new BA2ScanResult
            {
                FormatIssues = new List<BA2FormatIssue>
                {
                    new("C:\\bad.ba2", "bad.ba2", "BAAD")
                }
            }
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);

        // Verify order: header -> unpacked -> archived -> footer
        var headerIndex = content.IndexOf("Fallout 4");
        var unpackedIndex = content.IndexOf("Unpacked");
        var archivedIndex = content.IndexOf("Archived");
        var footerIndex = content.IndexOf("Scanner111");

        headerIndex.Should().BeLessThan(unpackedIndex);
        unpackedIndex.Should().BeLessThan(archivedIndex);
        archivedIndex.Should().BeLessThan(footerIndex);
    }

    [Fact]
    public void BuildCombinedReport_WithPartialResults_SkipsMissingSections()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Fallout 4",
            XseAcronym = "F4SE",
            UnpackedResult = new UnpackedScanResult
            {
                CleanupIssues = new List<CleanupIssue>
                {
                    new("C:\\readme.txt", "readme.txt", CleanupItemType.ReadmeFile)
                }
            }
            // No ArchivedResult
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Unpacked");
        content.Should().NotContain("## Results from Archived"); // Should not include archived header if no result
    }

    [Fact]
    public void BuildCombinedReport_WithNoIssues_IncludesSuccessMessage()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Fallout 4",
            XseAcronym = "F4SE",
            UnpackedResult = new UnpackedScanResult(), // No issues
            ArchivedResult = new BA2ScanResult() // No issues
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("No issues found");
        content.Should().Contain("✅");
    }

    [Fact]
    public void BuildCombinedReport_AlwaysIncludesHeaderAndFooter()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Skyrim Special Edition",
            XseAcronym = "SKSE64",
            ScanTimestamp = DateTimeOffset.Now
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Skyrim Special Edition");
        content.Should().Contain("Scanner111");
    }

    [Fact]
    public void BuildCombinedReport_WithConfigIssues_IncludesConfigurationSection()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Fallout 4",
            XseAcronym = "F4SE",
            IniResult = new IniScanResult
            {
                ConfigIssues = new List<ConfigIssue>
                {
                    new("C:\\path", "fallout4.ini", "Archive", "test", "0", "1", "Test", ConfigIssueSeverity.Warning)
                }
            },
            TomlResult = new TomlScanResult
            {
                CrashGenName = "Buffout4",
                ConfigFileFound = false
            }
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Configuration Issues");
        content.Should().Contain("INI CONFIGURATION");
        content.Should().Contain("BUFFOUT4");
    }

    [Fact]
    public void BuildCombinedReport_SubstitutesXseAcronymCorrectly()
    {
        // Arrange
        var report = new ScanGameReport
        {
            GameName = "Skyrim Special Edition",
            XseAcronym = "SKSE64",
            UnpackedResult = new UnpackedScanResult
            {
                XseFileIssues = new List<UnpackedXseFileIssue>
                {
                    new("C:\\Scripts", "Scripts")
                }
            }
        };

        // Act
        var fragment = _builder.BuildCombinedReport(report);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("SKSE64");
        content.Should().NotContain("F4SE");
    }

    #endregion
}
