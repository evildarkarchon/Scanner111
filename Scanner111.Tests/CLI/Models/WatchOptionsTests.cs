using FluentAssertions;
using Scanner111.CLI.Models;
using Xunit;

namespace Scanner111.Tests.CLI.Models;

/// <summary>
/// Unit tests for the WatchOptions model class
/// </summary>
public class WatchOptionsTests
{
    [Fact]
    public void WatchOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new WatchOptions();

        // Assert
        options.Path.Should().BeNull();
        options.Game.Should().BeNull();
        options.ScanExisting.Should().BeFalse();
        options.AutoMove.Should().BeFalse();
        options.ShowNotifications.Should().BeFalse(); // Default without CommandLineParser processing
        options.ShowDashboard.Should().BeFalse(); // Default without CommandLineParser processing
        options.Pattern.Should().Be("*.log");
        options.Recursive.Should().BeFalse();
        options.FcxMode.Should().BeFalse();
        options.Verbose.Should().BeFalse();
    }

    [Fact]
    public void WatchOptions_CanSetPath()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Path = "/test/path";

        // Assert
        options.Path.Should().Be("/test/path");
    }

    [Fact]
    public void WatchOptions_CanSetGame()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Game = "Fallout4";

        // Assert
        options.Game.Should().Be("Fallout4");
    }

    [Fact]
    public void WatchOptions_CanEnableScanExisting()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.ScanExisting = true;

        // Assert
        options.ScanExisting.Should().BeTrue();
    }

    [Fact]
    public void WatchOptions_CanEnableAutoMove()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.AutoMove = true;

        // Assert
        options.AutoMove.Should().BeTrue();
    }

    [Fact]
    public void WatchOptions_CanDisableNotifications()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.ShowNotifications = false;

        // Assert
        options.ShowNotifications.Should().BeFalse();
    }

    [Fact]
    public void WatchOptions_CanDisableDashboard()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.ShowDashboard = false;

        // Assert
        options.ShowDashboard.Should().BeFalse();
    }

    [Fact]
    public void WatchOptions_CanSetCustomPattern()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Pattern = "*.crash";

        // Assert
        options.Pattern.Should().Be("*.crash");
    }

    [Fact]
    public void WatchOptions_CanEnableRecursive()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Recursive = true;

        // Assert
        options.Recursive.Should().BeTrue();
    }

    [Fact]
    public void WatchOptions_CanEnableFcxMode()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.FcxMode = true;

        // Assert
        options.FcxMode.Should().BeTrue();
    }

    [Fact]
    public void WatchOptions_CanEnableVerbose()
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Verbose = true;

        // Assert
        options.Verbose.Should().BeTrue();
    }

    [Fact]
    public void WatchOptions_InitializationWithAllProperties()
    {
        // Arrange & Act
        var options = new WatchOptions
        {
            Path = "/watch/dir",
            Game = "Skyrim",
            ScanExisting = true,
            AutoMove = true,
            ShowNotifications = false,
            ShowDashboard = false,
            Pattern = "*.txt",
            Recursive = true,
            FcxMode = true,
            Verbose = true
        };

        // Assert
        options.Path.Should().Be("/watch/dir");
        options.Game.Should().Be("Skyrim");
        options.ScanExisting.Should().BeTrue();
        options.AutoMove.Should().BeTrue();
        options.ShowNotifications.Should().BeFalse();
        options.ShowDashboard.Should().BeFalse();
        options.Pattern.Should().Be("*.txt");
        options.Recursive.Should().BeTrue();
        options.FcxMode.Should().BeTrue();
        options.Verbose.Should().BeTrue();
    }

    [Theory]
    [InlineData("*.log", "Standard log files")]
    [InlineData("*.crash", "Crash dump files")]
    [InlineData("crash-*.log", "Specific crash log pattern")]
    [InlineData("*.txt", "Text files")]
    [InlineData("*", "All files")]
    public void WatchOptions_PatternSupportsVariousFormats(string pattern, string description)
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Pattern = pattern;

        // Assert
        options.Pattern.Should().Be(pattern, $"should support {description}");
    }

    [Theory]
    [InlineData("Fallout4")]
    [InlineData("fallout4")]
    [InlineData("FALLOUT4")]
    [InlineData("Skyrim")]
    [InlineData("skyrim")]
    [InlineData("SKYRIM")]
    public void WatchOptions_GameSupportsVariousCasings(string gameName)
    {
        // Arrange
        var options = new WatchOptions();

        // Act
        options.Game = gameName;

        // Assert
        options.Game.Should().Be(gameName);
    }

    [Fact]
    public void WatchOptions_PathCanBeNullOrEmpty()
    {
        // Arrange
        var options1 = new WatchOptions { Path = null };
        var options2 = new WatchOptions { Path = "" };
        var options3 = new WatchOptions { Path = "   " };

        // Assert
        options1.Path.Should().BeNull();
        options2.Path.Should().BeEmpty();
        options3.Path.Should().Be("   ");
    }

    [Fact]
    public void WatchOptions_CombinedOptionsForTypicalScenarios()
    {
        // Scenario 1: Watch Fallout 4 logs with auto-move
        var fallout4Options = new WatchOptions
        {
            Game = "Fallout4",
            AutoMove = true,
            ScanExisting = true
        };
        
        fallout4Options.Game.Should().Be("Fallout4");
        fallout4Options.AutoMove.Should().BeTrue();
        fallout4Options.ScanExisting.Should().BeTrue();
        fallout4Options.ShowDashboard.Should().BeFalse(); // Default without CommandLineParser

        // Scenario 2: Silent monitoring with custom path
        var silentOptions = new WatchOptions
        {
            Path = "/custom/logs",
            ShowNotifications = false,
            ShowDashboard = false
        };
        
        silentOptions.Path.Should().Be("/custom/logs");
        silentOptions.ShowNotifications.Should().BeFalse();
        silentOptions.ShowDashboard.Should().BeFalse();

        // Scenario 3: Recursive FCX monitoring
        var fcxOptions = new WatchOptions
        {
            Recursive = true,
            FcxMode = true,
            Verbose = true
        };
        
        fcxOptions.Recursive.Should().BeTrue();
        fcxOptions.FcxMode.Should().BeTrue();
        fcxOptions.Verbose.Should().BeTrue();
    }
}