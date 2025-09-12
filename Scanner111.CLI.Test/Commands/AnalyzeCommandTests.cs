using CommandLine;
using FluentAssertions;
using Scanner111.CLI.Commands;
using Scanner111.Core.Reporting;
using Xunit;

namespace Scanner111.CLI.Test.Commands;

public class AnalyzeCommandTests
{
    private readonly Parser _parser;

    public AnalyzeCommandTests()
    {
        _parser = new Parser(settings =>
        {
            settings.CaseSensitive = false;
            settings.HelpWriter = null;
        });
    }

    [Fact]
    public void Parse_WithRequiredLogFile_ParsesSuccessfully()
    {
        // Arrange
        var args = new[] { "analyze", "test.log" };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        result.As<Parsed<AnalyzeCommand>>().Value.LogFile.Should().Be("test.log");
    }

    [Fact]
    public void Parse_WithAllOptions_ParsesAllValues()
    {
        // Arrange
        var args = new[]
        {
            "analyze",
            "test.log",
            "--analyzers", "PluginAnalyzer,SettingsAnalyzer",
            "--output", "report.html",
            "--format", "html",
            "--verbose"
        };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        var command = result.As<Parsed<AnalyzeCommand>>().Value;
        
        command.LogFile.Should().Be("test.log");
        command.Analyzers.Should().BeEquivalentTo(new[] { "PluginAnalyzer", "SettingsAnalyzer" });
        command.OutputFile.Should().Be("report.html");
        command.Format.Should().Be(ReportFormat.Html);
        command.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithoutRequiredLogFile_FailsParsing()
    {
        // Arrange
        var args = new[] { "analyze" };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<NotParsed<AnalyzeCommand>>();
    }

    [Fact]
    public void Parse_WithShortOptions_ParsesSuccessfully()
    {
        // Arrange
        var args = new[]
        {
            "analyze",
            "test.log",
            "-a", "all",
            "-o", "output.txt",
            "-f", "text",
            "-v"
        };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        var command = result.As<Parsed<AnalyzeCommand>>().Value;
        
        command.LogFile.Should().Be("test.log");
        command.Analyzers.Should().BeEquivalentTo(new[] { "all" });
        command.OutputFile.Should().Be("output.txt");
        command.Format.Should().Be(ReportFormat.PlainText);
        command.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        var args = new[] { "analyze", "test.log" };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        var command = result.As<Parsed<AnalyzeCommand>>().Value;
        
        command.Analyzers.Should().BeNull();
        command.OutputFile.Should().BeNull();
        command.Format.Should().Be(ReportFormat.Markdown); // Default value
        command.Verbose.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithMultipleAnalyzers_ParsesAsArray()
    {
        // Arrange
        var args = new[]
        {
            "analyze",
            "test.log",
            "--analyzers", "Analyzer1,Analyzer2,Analyzer3"
        };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        var command = result.As<Parsed<AnalyzeCommand>>().Value;
        
        command.Analyzers.Should().HaveCount(3);
        command.Analyzers.Should().BeEquivalentTo(new[] { "Analyzer1", "Analyzer2", "Analyzer3" });
    }

    [Fact]
    public void Parse_WithInvalidFormat_StillParsesValue()
    {
        // Arrange
        var args = new[]
        {
            "analyze",
            "test.log",
            "--format", "invalid"
        };

        // Act
        var result = _parser.ParseArguments<AnalyzeCommand>(args);

        // Assert
        result.Should().BeOfType<Parsed<AnalyzeCommand>>();
        var command = result.As<Parsed<AnalyzeCommand>>().Value;
        // Format should have defaulted to Markdown since "invalid" is not a valid enum value
        command.Format.Should().Be(ReportFormat.Markdown);
    }

    [Fact]
    public void LogFileProperty_HasCorrectAttributes()
    {
        // Arrange
        var property = typeof(AnalyzeCommand).GetProperty(nameof(AnalyzeCommand.LogFile));
        var valueAttribute = property?.GetCustomAttributes(typeof(ValueAttribute), false)
            .FirstOrDefault() as ValueAttribute;

        // Assert
        valueAttribute.Should().NotBeNull();
        valueAttribute!.Index.Should().Be(0);
        valueAttribute.Required.Should().BeTrue();
        valueAttribute.HelpText.Should().Contain("log file");
    }

    [Fact]
    public void AnalyzersProperty_HasCorrectAttributes()
    {
        // Arrange
        var property = typeof(AnalyzeCommand).GetProperty(nameof(AnalyzeCommand.Analyzers));
        var optionAttribute = property?.GetCustomAttributes(typeof(OptionAttribute), false)
            .FirstOrDefault() as OptionAttribute;

        // Assert
        optionAttribute.Should().NotBeNull();
        optionAttribute!.ShortName.Should().Be("a");
        optionAttribute.LongName.Should().Be("analyzers");
        optionAttribute.Required.Should().BeFalse();
    }
}