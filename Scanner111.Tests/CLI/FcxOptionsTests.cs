using CommandLine;
using Scanner111.CLI.Models;
using Xunit;

namespace Scanner111.Tests.CLI;

public class FcxOptionsTests
{
    private readonly Parser _parser;
    
    public FcxOptionsTests()
    {
        _parser = new Parser(with => with.HelpWriter = null);
    }
    
    [Fact]
    public void Parse_FcxVerb_CreatesCorrectOptions()
    {
        // Arrange
        var args = new[] { "fcx", "--game", "Fallout4" };
        
        // Act
        var result = _parser.ParseArguments<FcxOptions>(args);
        
        // Assert
        Assert.True(result.Tag == ParserResultType.Parsed);
        result.WithParsed<FcxOptions>(opts =>
        {
            Assert.Equal("Fallout4", opts.Game);
        });
    }
    
    [Fact]
    public void Parse_FcxWithAllOptions_ParsesCorrectly()
    {
        // Arrange
        var args = new[]
        {
            "fcx",
            "--game", "Fallout4",
            "--check-only",
            "--validate-hashes",
            "--check-mods",
            "--check-ini",
            "--backup",
            "--game-path", @"C:\Games\Fallout4",
            "--mods-folder", @"C:\Mods",
            "--ini-folder", @"C:\Ini",
            "-v",
            "--disable-colors",
            "--disable-progress",
            "-o", "results.txt"
        };
        
        // Act
        var result = _parser.ParseArguments<FcxOptions>(args);
        
        // Assert
        Assert.True(result.Tag == ParserResultType.Parsed);
        result.WithParsed<FcxOptions>(opts =>
        {
            Assert.Equal("Fallout4", opts.Game);
            Assert.True(opts.CheckOnly);
            Assert.True(opts.ValidateHashes);
            Assert.True(opts.CheckMods);
            Assert.True(opts.CheckIni);
            Assert.True(opts.Backup);
            Assert.Equal(@"C:\Games\Fallout4", opts.GamePath);
            Assert.Equal(@"C:\Mods", opts.ModsFolder);
            Assert.Equal(@"C:\Ini", opts.IniFolder);
            Assert.True(opts.Verbose);
            Assert.True(opts.DisableColors);
            Assert.True(opts.DisableProgress);
            Assert.Equal("results.txt", opts.OutputFile);
        });
    }
    
    [Fact]
    public void Parse_FcxWithRestore_ParsesRestorePath()
    {
        // Arrange
        var args = new[] { "fcx", "--restore", @"C:\Backups\backup.zip" };
        
        // Act
        var result = _parser.ParseArguments<FcxOptions>(args);
        
        // Assert
        Assert.True(result.Tag == ParserResultType.Parsed);
        result.WithParsed<FcxOptions>(opts =>
        {
            Assert.Equal(@"C:\Backups\backup.zip", opts.RestorePath);
        });
    }
    
    [Fact]
    public void Parse_FcxWithShortOptions_ParsesCorrectly()
    {
        // Arrange
        var args = new[]
        {
            "fcx",
            "-g", "Fallout4",
            "-v",
            "-o", "output.txt"
        };
        
        // Act
        var result = _parser.ParseArguments<FcxOptions>(args);
        
        // Assert
        Assert.True(result.Tag == ParserResultType.Parsed);
        result.WithParsed<FcxOptions>(opts =>
        {
            Assert.Equal("Fallout4", opts.Game);
            Assert.True(opts.Verbose);
            Assert.Equal("output.txt", opts.OutputFile);
        });
    }
    
    [Fact]
    public void Parse_FcxWithoutOptions_UsesDefaults()
    {
        // Arrange
        var args = new[] { "fcx" };
        
        // Act
        var result = _parser.ParseArguments<FcxOptions>(args);
        
        // Assert
        Assert.True(result.Tag == ParserResultType.Parsed);
        result.WithParsed<FcxOptions>(opts =>
        {
            Assert.Equal("Fallout4", opts.Game); // Default value
            Assert.False(opts.CheckOnly);
            Assert.False(opts.ValidateHashes);
            Assert.False(opts.CheckMods);
            Assert.False(opts.CheckIni);
            Assert.False(opts.Backup);
            Assert.Null(opts.RestorePath);
            Assert.Null(opts.GamePath);
            Assert.Null(opts.ModsFolder);
            Assert.Null(opts.IniFolder);
            Assert.False(opts.Verbose);
            Assert.False(opts.DisableColors);
            Assert.False(opts.DisableProgress);
            Assert.Null(opts.OutputFile);
        });
    }
    
    [Fact] 
    public void Parse_WithoutVerb_UsesDefaultScanVerb()
    {
        // Arrange
        var args = new[] { "-l", "some-file.log" };
        
        // Act
        // Since "scan" is the default verb, we can use scan options without specifying the verb
        var result = _parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions>(args);
        
        // Assert
        Assert.Equal(ParserResultType.Parsed, result.Tag);
        result.WithParsed<ScanOptions>(opts =>
        {
            Assert.Equal("some-file.log", opts.LogFile);
        });
    }
}