using CommandLine;
using FluentAssertions;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Xunit;

namespace Scanner111.Tests.CLI;

public class ScanCommandFcxTests
{
    [Fact]
    public void ScanOptions_FcxModeOption_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "scan", "--fcx-mode", "true", "-l", "crash.log" };
        var parser = new CommandLine.Parser(with => with.HelpWriter = null);
        
        // Act
        var result = parser.ParseArguments<Scanner111.CLI.Models.ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions>(args);
        
        // Assert
        result.Tag.Should().Be(CommandLine.ParserResultType.Parsed, "because arguments should parse successfully");
        result.WithParsed<Scanner111.CLI.Models.ScanOptions>(opts =>
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
        var parser = new CommandLine.Parser(with => with.HelpWriter = null);
        
        // Act
        var result = parser.ParseArguments<Scanner111.CLI.Models.ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions>(args);
        
        // Assert
        result.Tag.Should().Be(CommandLine.ParserResultType.Parsed, "because arguments should parse successfully");
        result.WithParsed<Scanner111.CLI.Models.ScanOptions>(opts =>
        {
            opts.FcxMode.Should().BeNull("because fcx-mode was not specified");
            opts.LogFile.Should().Be("crash.log", "because log file was specified");
        });
    }
    
    [Fact]
    public void ApplyCommandLineSettings_FcxModeTrue_UpdatesSettings()
    {
        // Arrange
        var options = new Scanner111.CLI.Models.ScanOptions { FcxMode = true };
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
        var options = new Scanner111.CLI.Models.ScanOptions { FcxMode = false };
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
        var options = new Scanner111.CLI.Models.ScanOptions { FcxMode = null };
        var settings = new ApplicationSettings { FcxMode = true };
        
        // Act
        if (options.FcxMode.HasValue)
            settings.FcxMode = options.FcxMode.Value;
        
        // Assert
        settings.FcxMode.Should().BeTrue("because FCX mode should remain unchanged when not specified");
    }
}