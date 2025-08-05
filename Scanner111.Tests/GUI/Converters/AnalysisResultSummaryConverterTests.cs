using System;
using System.Collections.Generic;
using System.Globalization;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.GUI.Converters;
using Xunit;

namespace Scanner111.Tests.GUI.Converters;

public class AnalysisResultSummaryConverterTests
{
    private readonly AnalysisResultSummaryConverter _converter = new();

    [Fact]
    public void Convert_NullValue_ReturnsDefaultMessage()
    {
        // Act
        var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("No analysis data available", result);
    }

    [Fact]
    public void Convert_NotAnalysisResult_ReturnsDefaultMessage()
    {
        // Act
        var result = _converter.Convert("not an analysis result", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("No analysis data available", result);
    }

    [Theory]
    [InlineData("FormIdAnalyzer", "No problematic FormIDs detected")]
    [InlineData("PluginAnalyzer", "No plugin conflicts found")]
    [InlineData("SuspectScanner", "No known error patterns detected")]
    [InlineData("RecordScanner", "No corrupted records found")]
    [InlineData("SettingsScanner", "No configuration issues detected")]
    [InlineData("StackAnalyzer", "No stack trace issues found")]
    [InlineData("UnknownAnalyzer", "Analysis completed successfully")]
    public void Convert_NoFindings_ReturnsAppropriateMessage(string analyzerName, string expectedMessage)
    {
        // Arrange
        var result = new TestAnalysisResult
        {
            AnalyzerName = analyzerName,
            HasFindings = false
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expectedMessage, convertedResult);
    }

    [Fact]
    public void Convert_FormIdResult_WithUnresolvedFormIds_ReturnsSummary()
    {
        // Arrange
        var result = new FormIdAnalysisResult
        {
            AnalyzerName = "FormIdAnalyzer",
            HasFindings = true,
            FormIds = new List<string> { "00001234", "00005678", "00009ABC" },
            ResolvedFormIds = new List<FormId> { new FormId { Id = "00001234", Description = "Item1" } }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Found 3 FormIDs - 2 unresolved (potential issues)", convertedResult);
    }

    [Fact]
    public void Convert_FormIdResult_AllResolved_ReturnsSummary()
    {
        // Arrange
        var result = new FormIdAnalysisResult
        {
            AnalyzerName = "FormIdAnalyzer",
            HasFindings = true,
            FormIds = new List<string> { "00001234", "00005678" },
            ResolvedFormIds = new List<FormId> 
            { 
                new FormId { Id = "00001234", Description = "Item1" },
                new FormId { Id = "00005678", Description = "Item2" }
            }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Found 2 FormIDs - all resolved successfully", convertedResult);
    }

    [Fact]
    public void Convert_FormIdResult_NoFormIds_ReturnsNoFormIdsMessage()
    {
        // Arrange
        var result = new FormIdAnalysisResult
        {
            AnalyzerName = "FormIdAnalyzer",
            HasFindings = true,
            FormIds = new List<string>(),
            ResolvedFormIds = new List<FormId>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("No FormIDs found in crash log", convertedResult);
    }

    [Fact]
    public void Convert_PluginResult_WithSuspectedPlugins_ReturnsSummary()
    {
        // Arrange
        var result = new PluginAnalysisResult
        {
            AnalyzerName = "PluginAnalyzer",
            HasFindings = true,
            Plugins = new List<Plugin> 
            { 
                new Plugin { FileName = "Plugin1.esp" }, 
                new Plugin { FileName = "Plugin2.esp" }, 
                new Plugin { FileName = "Plugin3.esp" } 
            },
            SuspectedPlugins = new List<Plugin> { new Plugin { FileName = "Plugin2.esp" } }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Found 3 plugins - 1 potentially problematic", convertedResult);
    }

    [Fact]
    public void Convert_PluginResult_NoSuspectedPlugins_ReturnsSummary()
    {
        // Arrange
        var result = new PluginAnalysisResult
        {
            AnalyzerName = "PluginAnalyzer",
            HasFindings = true,
            Plugins = new List<Plugin> 
            { 
                new Plugin { FileName = "Plugin1.esp" }, 
                new Plugin { FileName = "Plugin2.esp" } 
            },
            SuspectedPlugins = new List<Plugin>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Found 2 plugins - no obvious conflicts", convertedResult);
    }

    [Fact]
    public void Convert_PluginResult_NoPlugins_ReturnsNoPluginsMessage()
    {
        // Arrange
        var result = new PluginAnalysisResult
        {
            AnalyzerName = "PluginAnalyzer",
            HasFindings = true,
            Plugins = new List<Plugin>(),
            SuspectedPlugins = new List<Plugin>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("No plugins detected in crash log", convertedResult);
    }

    [Fact]
    public void Convert_SuspectResult_WithBothErrorAndStackMatches_ReturnsSummary()
    {
        // Arrange
        var result = new SuspectAnalysisResult
        {
            AnalyzerName = "SuspectScanner",
            HasFindings = true,
            ErrorMatches = new List<string> 
            { 
                "Error 1: Info 1",
                "Error 2: Info 2"
            },
            StackMatches = new List<string> 
            { 
                "Stack 1: Info 1"
            }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Detected 2 error pattern(s) and 1 stack pattern(s)", convertedResult);
    }

    [Fact]
    public void Convert_SuspectResult_OnlyErrorMatches_ReturnsSummary()
    {
        // Arrange
        var result = new SuspectAnalysisResult
        {
            AnalyzerName = "SuspectScanner",
            HasFindings = true,
            ErrorMatches = new List<string> 
            { 
                "Error 1: Info 1"
            },
            StackMatches = new List<string>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Detected 1 error pattern(s)", convertedResult);
    }

    [Fact]
    public void Convert_SuspectResult_OnlyStackMatches_ReturnsSummary()
    {
        // Arrange
        var result = new SuspectAnalysisResult
        {
            AnalyzerName = "SuspectScanner",
            HasFindings = true,
            ErrorMatches = new List<string>(),
            StackMatches = new List<string> 
            { 
                "Stack 1: Info 1",
                "Stack 2: Info 2"
            }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Detected 2 stack pattern(s)", convertedResult);
    }

    [Fact]
    public void Convert_SuspectResult_NoMatches_ReturnsNoPatternMessage()
    {
        // Arrange
        var result = new SuspectAnalysisResult
        {
            AnalyzerName = "SuspectScanner",
            HasFindings = true,
            ErrorMatches = new List<string>(),
            StackMatches = new List<string>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("No known error patterns detected", convertedResult);
    }

    [Fact]
    public void Convert_GenericResult_WithReportLines_ReturnsSummary()
    {
        // Arrange
        var result = new TestAnalysisResult
        {
            AnalyzerName = "GenericAnalyzer",
            HasFindings = true,
            ReportLines = new List<string> { "  First finding  ", "Second finding" }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("2 finding(s) - First finding", convertedResult);
    }

    [Fact]
    public void Convert_GenericResult_WithEmptyReportLines_ReturnsGenericMessage()
    {
        // Arrange
        var result = new TestAnalysisResult
        {
            AnalyzerName = "GenericAnalyzer",
            HasFindings = true,
            ReportLines = new List<string>()
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Analysis completed - see full report for details", convertedResult);
    }

    [Fact]
    public void Convert_GenericResult_WithEmptyFirstLine_ReturnsCountOnly()
    {
        // Arrange
        var result = new TestAnalysisResult
        {
            AnalyzerName = "GenericAnalyzer",
            HasFindings = true,
            ReportLines = new List<string> { "   ", "Second finding" }
        };

        // Act
        var convertedResult = _converter.Convert(result, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Analysis completed with 2 finding(s)", convertedResult);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack("Some text", typeof(AnalysisResult), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = AnalysisResultSummaryConverter.Instance;
        var instance2 = AnalysisResultSummaryConverter.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    // Test implementation of AnalysisResult for testing purposes
    private class TestAnalysisResult : AnalysisResult
    {
    }
}