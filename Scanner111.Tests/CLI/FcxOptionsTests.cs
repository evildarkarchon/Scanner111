using CommandLine;
using FluentAssertions;
using Scanner111.CLI.Models;

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
        result.Tag.Should().Be(ParserResultType.Parsed, "because FCX command should parse successfully");
        result.WithParsed<FcxOptions>(opts => { opts.Game.Should().Be("Fallout4", "because game was specified"); });
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
        result.Tag.Should().Be(ParserResultType.Parsed, "because all options should parse successfully");
        result.WithParsed<FcxOptions>(opts =>
        {
            opts.Game.Should().Be("Fallout4", "because game was specified");
            opts.CheckOnly.Should().BeTrue("because check-only flag was set");
            opts.ValidateHashes.Should().BeTrue("because validate-hashes flag was set");
            opts.CheckMods.Should().BeTrue("because check-mods flag was set");
            opts.CheckIni.Should().BeTrue("because check-ini flag was set");
            opts.Backup.Should().BeTrue("because backup flag was set");
            opts.GamePath.Should().Be(@"C:\Games\Fallout4", "because game path was specified");
            opts.ModsFolder.Should().Be(@"C:\Mods", "because mods folder was specified");
            opts.IniFolder.Should().Be(@"C:\Ini", "because ini folder was specified");
            opts.Verbose.Should().BeTrue("because verbose flag was set");
            opts.DisableColors.Should().BeTrue("because disable-colors flag was set");
            opts.DisableProgress.Should().BeTrue("because disable-progress flag was set");
            opts.OutputFile.Should().Be("results.txt", "because output file was specified");
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
        result.Tag.Should().Be(ParserResultType.Parsed, "because restore option should parse successfully");
        result.WithParsed<FcxOptions>(opts =>
        {
            opts.RestorePath.Should().Be(@"C:\Backups\backup.zip", "because restore path was specified");
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
        result.Tag.Should().Be(ParserResultType.Parsed, "because short options should parse successfully");
        result.WithParsed<FcxOptions>(opts =>
        {
            opts.Game.Should().Be("Fallout4", "because game was specified with -g");
            opts.Verbose.Should().BeTrue("because verbose was set with -v");
            opts.OutputFile.Should().Be("output.txt", "because output file was specified with -o");
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
        result.Tag.Should().Be(ParserResultType.Parsed, "because FCX without options should use defaults");
        result.WithParsed<FcxOptions>(opts =>
        {
            opts.Game.Should().Be("Fallout4", "because it's the default game");
            opts.CheckOnly.Should().BeFalse("because it's not set by default");
            opts.ValidateHashes.Should().BeFalse("because it's not set by default");
            opts.CheckMods.Should().BeFalse("because it's not set by default");
            opts.CheckIni.Should().BeFalse("because it's not set by default");
            opts.Backup.Should().BeFalse("because it's not set by default");
            opts.RestorePath.Should().BeNull("because no restore path was specified");
            opts.GamePath.Should().BeNull("because no game path was specified");
            opts.ModsFolder.Should().BeNull("because no mods folder was specified");
            opts.IniFolder.Should().BeNull("because no ini folder was specified");
            opts.Verbose.Should().BeFalse("because it's not set by default");
            opts.DisableColors.Should().BeFalse("because it's not set by default");
            opts.DisableProgress.Should().BeFalse("because it's not set by default");
            opts.OutputFile.Should().BeNull("because no output file was specified");
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
        result.Tag.Should().Be(ParserResultType.Parsed, "because scan is the default verb");
        result.WithParsed<ScanOptions>(opts =>
        {
            opts.LogFile.Should().Be("some-file.log", "because log file was specified");
        });
    }
}