using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for SettingsScanner.
/// </summary>
public class SettingsScannerTests
{
    private readonly SettingsScanner _scanner;

    public SettingsScannerTests()
    {
        _scanner = new SettingsScanner();
    }

    [Fact]
    public async Task ScanAsync_WithCorrectSettings_NoMisconfigurations()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "MemoryManager: false",
                "AutoScanning: true",
                "F4SE: true"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.Misconfigurations.Should().BeEmpty();
        result.DetectedSettings.Should().ContainKey("MemoryManager");
        result.DetectedSettings["MemoryManager"].Should().Be("false");
    }

    [Fact]
    public async Task ScanAsync_WithIncorrectSetting_DetectsMisconfiguration()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "MemoryManager: true",  // Should be false
                "AutoScanning: true"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.Misconfigurations.Should().NotBeEmpty();
        result.Misconfigurations.Should().Contain(m => m.Contains("MemoryManager"));
    }

    [Fact]
    public async Task ScanAsync_WithMissingSetting_DetectsMisconfiguration()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "AutoScanning: true"
                // MemoryManager is missing
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.Misconfigurations.Should().Contain(m => m.Contains("MemoryManager"));
        result.Misconfigurations.Should().Contain(m => m.Contains("not found"));
    }

    [Fact]
    public async Task ScanAsync_WithNullSegment_ReturnsWarning()
    {
        // Arrange
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(null, expectedSettings);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("No Compatibility segment"));
    }

    [Fact]
    public async Task ScanAsync_DetectsVersion()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "Buffout 4 v1.26.2",
                "MemoryManager: false"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.DetectedVersion.Should().Be("Buffout 4 v1.26.2");
    }

    [Fact]
    public async Task ScanAsync_WithOutdatedVersion_WarnsAboutIt()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "Buffout 4 v1.0.0",  // Older than default (1.28.6)
                "MemoryManager: false"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.IsOutdated.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Outdated"));
    }

    [Fact]
    public async Task ScanAsync_WithCurrentVersion_NoOutdatedWarning()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "Buffout 4 v1.28.6",  // Current version
                "MemoryManager: false"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.IsOutdated.Should().BeFalse();
    }

    [Fact]
    public void GameSettings_CreateFallout4Defaults_HasCorrectValues()
    {
        // Act
        var settings = GameSettings.CreateFallout4Defaults();

        // Assert
        settings.GameName.Should().Be("Fallout 4");
        settings.RecommendedSettings.Should().ContainKey("MemoryManager");
        settings.RecommendedSettings["MemoryManager"].Should().Be("false");
    }

    [Fact]
    public void GameSettings_CreateSkyrimDefaults_HasCorrectValues()
    {
        // Act
        var settings = GameSettings.CreateSkyrimDefaults();

        // Assert
        settings.GameName.Should().Be("Skyrim Special Edition");
        settings.RecommendedSettings.Should().ContainKey("AutoScanning");
    }

    [Fact]
    public async Task ScanAsync_IsCaseInsensitive()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "memorymanager: FALSE",  // Different case
                "autoscanning: TRUE",
                "f4se: TRUE"
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.Misconfigurations.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithWhitespace_ParsesCorrectly()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "Compatibility",
            Lines = new[]
            {
                "  MemoryManager  :  false  ",
                "   AutoScanning:true   "
            }
        };
        var expectedSettings = GameSettings.CreateFallout4Defaults();

        // Act
        var result = await _scanner.ScanAsync(segment, expectedSettings);

        // Assert
        result.DetectedSettings.Should().ContainKey("MemoryManager");
        result.DetectedSettings["MemoryManager"].Should().Be("false");
    }
}
