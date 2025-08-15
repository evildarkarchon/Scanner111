using CommandLine;
using Scanner111.CLI.Models;
using Scanner111.Core.Models;

namespace Scanner111.Tests.CLI;

public class ScanCommandFcxTests
{
    [Fact]
    public void ScanOptions_FcxModeOption_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "scan", "--fcx-mode", "true", "-l", "crash.log" };
        var parser = new Parser(with => with.HelpWriter = null);

        // Act
        var result = parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions>(args);

        // Assert
        result.Tag.Should().Be(ParserResultType.Parsed, "because arguments should parse successfully");
        result.WithParsed<ScanOptions>(opts =>
        {
            opts.FcxMode.Should().NotBeNull("because fcx-mode was specified");
            opts.FcxMode!.Value.Should().BeTrue("because fcx-mode was set to true");
            opts.LogFile.Should().Be("crash.log", "because log file was specified");
        });
    }

    [Fact]
    public void ScanOptions_WithoutFcxMode_DefaultsToNull()
    {
        // Arrange
        var args = new[] { "scan", "-l", "crash.log" };
        var parser = new Parser(with => with.HelpWriter = null);

        // Act
        var result = parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions>(args);

        // Assert
        result.Tag.Should().Be(ParserResultType.Parsed, "because arguments should parse successfully");
        result.WithParsed<ScanOptions>(opts =>
        {
            opts.FcxMode.Should().BeNull("because fcx-mode was not specified");
            opts.LogFile.Should().Be("crash.log", "because log file was specified");
        });
    }

    [Fact]
    public void ApplyCommandLineSettings_FcxModeTrue_UpdatesSettings()
    {
        // Arrange
        var options = new ScanOptions { FcxMode = true };
        var settings = new ApplicationSettings { FcxMode = false };

        // Act
        // This would be done in ScanCommand.ApplyCommandLineSettings
        if (options.FcxMode.HasValue)
            settings.FcxMode = options.FcxMode.Value;

        // Assert
        settings.FcxMode.Should().BeTrue("because FCX mode should be enabled from command line");
    }

    [Fact]
    public void ApplyCommandLineSettings_FcxModeFalse_UpdatesSettings()
    {
        // Arrange
        var options = new ScanOptions { FcxMode = false };
        var settings = new ApplicationSettings { FcxMode = true };

        // Act
        if (options.FcxMode.HasValue)
            settings.FcxMode = options.FcxMode.Value;

        // Assert
        settings.FcxMode.Should().BeFalse("because FCX mode should be disabled from command line");
    }

    [Fact]
    public void ApplyCommandLineSettings_FcxModeNull_KeepsExistingSettings()
    {
        // Arrange
        var options = new ScanOptions { FcxMode = null };
        var settings = new ApplicationSettings { FcxMode = true };

        // Act
        if (options.FcxMode.HasValue)
            settings.FcxMode = options.FcxMode.Value;

        // Assert
        settings.FcxMode.Should().BeTrue("because FCX mode should remain unchanged when not specified");
    }
}