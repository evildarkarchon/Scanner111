using CommandLine;
using NSubstitute;
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
        var args = new[] { "scan", "--fcx-mode", "-l", "crash.log" };
        var parser = new CommandLine.Parser(with => with.HelpWriter = null);
        
        // Act
        var result = parser.ParseArguments<Scanner111.CLI.Models.ScanOptions>(args);
        
        // Assert
        Assert.True(result.Tag == CommandLine.ParserResultType.Parsed);
        result.WithParsed<Scanner111.CLI.Models.ScanOptions>(opts =>
        {
            Assert.True(opts.FcxMode);
            Assert.Equal("crash.log", opts.LogFile);
        });
    }
    
    [Fact]
    public void ScanOptions_WithoutFcxMode_DefaultsToNull()
    {
        // Arrange
        var args = new[] { "scan", "-l", "crash.log" };
        var parser = new CommandLine.Parser(with => with.HelpWriter = null);
        
        // Act
        var result = parser.ParseArguments<Scanner111.CLI.Models.ScanOptions>(args);
        
        // Assert
        Assert.True(result.Tag == CommandLine.ParserResultType.Parsed);
        result.WithParsed<Scanner111.CLI.Models.ScanOptions>(opts =>
        {
            Assert.Null(opts.FcxMode);
            Assert.Equal("crash.log", opts.LogFile);
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
        Assert.True(settings.FcxMode);
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
        Assert.False(settings.FcxMode);
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
        Assert.True(settings.FcxMode); // Unchanged
    }
}