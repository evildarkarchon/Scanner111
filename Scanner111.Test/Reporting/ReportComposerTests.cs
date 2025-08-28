using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Reporting;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Reporting")]
public class ReportComposerTests : IDisposable
{
    private readonly ILogger<ReportComposer> _logger;
    private readonly ReportComposer _composer;

    public ReportComposerTests()
    {
        _logger = Substitute.For<ILogger<ReportComposer>>();
        _composer = new ReportComposer(_logger);
    }

    #region ComposeReportAsync Tests

    [Fact]
    public async Task ComposeReportAsync_WithValidResults_ProducesReport()
    {
        // Arrange
        var results = new[]
        {
            CreateMockAnalysisResult("Analyzer1", true, 
                ReportFragment.CreateInfo("Info", "Info content")),
            CreateMockAnalysisResult("Analyzer2", true, 
                ReportFragment.CreateWarning("Warning", "Warning content"))
        };

        // Act
        var report = await _composer.ComposeReportAsync(results);

        // Assert
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("Info");
        report.Should().Contain("Warning");
    }

    [Fact]
    public async Task ComposeReportAsync_WithEmptyResults_ReturnsEmptyReport()
    {
        // Arrange
        var results = Array.Empty<AnalysisResult>();

        // Act
        var report = await _composer.ComposeReportAsync(results);

        // Assert
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("No results to report");
    }

    [Fact]
    public async Task ComposeReportAsync_WithNullResults_HandlesGracefully()
    {
        // Act
        var report = await _composer.ComposeReportAsync(null);

        // Assert
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("No results to report");
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("No analysis results")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ComposeReportAsync_WithSkippedAnalyzers_RespectsIncludeSkippedOption()
    {
        // Arrange
        var skippedResult = CreateMockAnalysisResult("SkippedAnalyzer", true, 
            ReportFragment.CreateWarning("Skipped", "Should be excluded"), 
            skipFurtherProcessing: true);
        
        var results = new[]
        {
            CreateMockAnalysisResult("Analyzer1", true, 
                ReportFragment.CreateInfo("Info", "Info content")),
            skippedResult
        };

        var optionsIncludeSkipped = new ReportOptions { IncludeSkipped = true };
        var optionsExcludeSkipped = new ReportOptions { IncludeSkipped = false };

        // Act
        var reportWithSkipped = await _composer.ComposeReportAsync(results, optionsIncludeSkipped);
        var reportWithoutSkipped = await _composer.ComposeReportAsync(results, optionsExcludeSkipped);

        // Assert
        reportWithSkipped.Should().Contain("Skipped");
        reportWithoutSkipped.Should().NotContain("Skipped");
    }

    [Fact]
    public async Task ComposeReportAsync_WithTimingInfo_IncludesPerformanceMetrics()
    {
        // Arrange
        var results = new[]
        {
            CreateMockAnalysisResult("FastAnalyzer", true, duration: TimeSpan.FromMilliseconds(50)),
            CreateMockAnalysisResult("SlowAnalyzer", true, duration: TimeSpan.FromMilliseconds(500))
        };

        var options = new ReportOptions { IncludeTimingInfo = true };

        // Act
        var report = await _composer.ComposeReportAsync(results, options);

        // Assert
        report.Should().Contain("Performance Metrics");
        report.Should().Contain("FastAnalyzer");
        report.Should().Contain("SlowAnalyzer");
        report.Should().Contain("Total Duration");
    }

    [Fact]
    public async Task ComposeReportAsync_WithErrors_CreatesErrorFragments()
    {
        // Arrange
        var failedResult = new AnalysisResult("FailedAnalyzer")
        {
            Success = false
        };
        failedResult.AddErrors(new[] { "Error 1", "Error 2" });
        
        var results = new[] { failedResult };

        // Act
        var report = await _composer.ComposeReportAsync(results);

        // Assert
        report.Should().Contain("FailedAnalyzer Errors");
        report.Should().Contain("Error 1");
        report.Should().Contain("Error 2");
    }

    #endregion

    #region ComposeFromFragmentsAsync Tests

    [Theory]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.PlainText)]
    public async Task ComposeFromFragmentsAsync_WithFormat_ProducesCorrectOutput(ReportFormat format)
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateHeader("Test Report"),
            ReportFragment.CreateSection("Section", "Content")
        };

        var options = new ReportOptions { Format = format };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(fragments, options);

        // Assert
        report.Should().NotBeNullOrEmpty();
        
        switch (format)
        {
            case ReportFormat.Markdown:
                report.Should().Contain("#");
                break;
            case ReportFormat.Html:
                report.Should().Contain("<html>");
                report.Should().Contain("</html>");
                break;
            case ReportFormat.Json:
                var action = () => JsonSerializer.Deserialize<object>(report);
                action.Should().NotThrow();
                break;
            case ReportFormat.PlainText:
                report.Should().NotContain("#");
                report.Should().NotContain("<");
                break;
        }
    }

    [Fact]
    public async Task ComposeFromFragmentsAsync_WithSorting_OrdersByPriority()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateInfo("Last", "Content", 300),
            ReportFragment.CreateError("First", "Content", 10),
            ReportFragment.CreateWarning("Middle", "Content", 50)
        };

        var options = new ReportOptions { SortByOrder = true };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(fragments, options);

        // Assert
        var lines = report.Split('\n');
        var firstIndex = Array.FindIndex(lines, l => l.Contains("First"));
        var middleIndex = Array.FindIndex(lines, l => l.Contains("Middle"));
        var lastIndex = Array.FindIndex(lines, l => l.Contains("Last"));

        firstIndex.Should().BeLessThan(middleIndex);
        middleIndex.Should().BeLessThan(lastIndex);
    }

    [Fact]
    public async Task ComposeFromFragmentsAsync_WithVisibilityFilter_ExcludesHiddenFragments()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateInfo("Always Visible", "Content"),
            ReportFragment.CreateConditional("Verbose Only", "Content", 
                FragmentVisibility.Verbose),
            ReportFragment.CreateConditional("Hidden", "Content", 
                FragmentVisibility.Hidden)
        };

        var optionsAll = new ReportOptions { MinimumVisibility = FragmentVisibility.Hidden };
        var optionsVerbose = new ReportOptions { MinimumVisibility = FragmentVisibility.Verbose };
        var optionsNormal = new ReportOptions { MinimumVisibility = FragmentVisibility.Always };

        // Act
        var reportAll = await _composer.ComposeFromFragmentsAsync(fragments, optionsAll);
        var reportVerbose = await _composer.ComposeFromFragmentsAsync(fragments, optionsVerbose);
        var reportNormal = await _composer.ComposeFromFragmentsAsync(fragments, optionsNormal);

        // Assert
        reportAll.Should().Contain("Always Visible");
        reportAll.Should().Contain("Verbose Only");
        reportAll.Should().Contain("Hidden");

        reportVerbose.Should().Contain("Always Visible");
        reportVerbose.Should().Contain("Verbose Only");
        reportVerbose.Should().NotContain("Hidden");

        reportNormal.Should().Contain("Always Visible");
        reportNormal.Should().NotContain("Verbose Only");
        reportNormal.Should().NotContain("Hidden");
    }

    [Fact]
    public async Task ComposeFromFragmentsAsync_WithTitle_IncludesTitle()
    {
        // Arrange
        var fragments = new[] { ReportFragment.CreateInfo("Content", "Some content") };
        var options = new ReportOptions { Title = "Custom Report Title" };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(fragments, options);

        // Assert
        report.Should().Contain("Custom Report Title");
        report.Should().Contain("Generated at");
    }

    #endregion

    #region Format-Specific Tests

    [Fact]
    public async Task FormatAsMarkdown_WithMetadata_GeneratesValidMarkdown()
    {
        // Arrange
        // Note: Current implementation doesn't support setting metadata after creation
        // This is a known limitation of the immutable design
        var fragment = ReportFragment.CreateSection("Test", "Content");
        
        var options = new ReportOptions 
        { 
            Format = ReportFormat.Markdown,
            IncludeMetadata = true 
        };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(new[] { fragment }, options);

        // Assert
        report.Should().Contain("###");
        report.Should().Contain("Scanner111 Orchestrator");
    }

    [Fact]
    public async Task FormatAsHtml_WithStyling_ProducesValidHtml()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateError("Error", "Error content"),
            ReportFragment.CreateWarning("Warning", "Warning content"),
            ReportFragment.CreateInfo("Info", "Info content")
        };

        var options = new ReportOptions { Format = ReportFormat.Html };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(fragments, options);

        // Assert
        report.Should().StartWith("<!DOCTYPE html>");
        report.Should().Contain("<style>");
        report.Should().Contain("class=\"error\"");
        report.Should().Contain("class=\"warning\"");
        report.Should().Contain("class=\"info\"");
        report.Should().EndWith("</html>\r\n");
    }

    [Fact]
    public async Task FormatAsJson_WithNestedFragments_SerializesCorrectly()
    {
        // Arrange
        var child = ReportFragment.CreateInfo("Child", "Child content");
        var parent = ReportFragment.CreateWithChildren("Parent", new[] { child });
        
        var options = new ReportOptions { Format = ReportFormat.Json };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(new[] { parent }, options);

        // Assert
        var json = JsonDocument.Parse(report);
        json.RootElement.GetProperty("title").GetString().Should().NotBeNull();
        json.RootElement.GetProperty("generatedAt").GetDateTime().Should().BeCloseTo(
            DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        var fragments = json.RootElement.GetProperty("fragments").EnumerateArray().ToList();
        fragments.Should().HaveCount(1);
        fragments[0].GetProperty("Title").GetString().Should().Be("Parent");
    }

    [Fact]
    public async Task FormatAsPlainText_WithFragments_ProducesReadableText()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateHeader("Header"),
            ReportFragment.CreateSection("Section", "Section content")
        };

        var options = new ReportOptions 
        { 
            Format = ReportFormat.PlainText,
            Title = "Plain Text Report" 
        };

        // Act
        var report = await _composer.ComposeFromFragmentsAsync(fragments, options);

        // Assert
        report.Should().NotContain("#");
        report.Should().NotContain("<");
        report.Should().Contain("Plain Text Report");
        report.Should().Contain("====="); // Title underline
        report.Should().Contain("Header");
        report.Should().Contain("-----"); // Section underline
        report.Should().Contain("Section content");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ComposeReportAsync_ConcurrentCalls_IsThreadSafe()
    {
        // Arrange
        var results = Enumerable.Range(0, 10)
            .Select(i => CreateMockAnalysisResult($"Analyzer{i}", true))
            .ToArray();

        var tasks = new List<Task<string>>();

        // Act
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_composer.ComposeReportAsync(results));
        }

        var reports = await Task.WhenAll(tasks);

        // Assert
        reports.Should().HaveCount(50);
        reports.Should().OnlyContain(r => !string.IsNullOrEmpty(r));
        reports.Should().OnlyContain(r => r.Contains("Analyzer0"));
    }

    #endregion

    #region Helper Methods

    private static AnalysisResult CreateMockAnalysisResult(
        string analyzerName,
        bool success = true,
        ReportFragment? fragment = null,
        TimeSpan? duration = null,
        bool skipFurtherProcessing = false)
    {
        var result = new AnalysisResult(analyzerName)
        {
            Success = success,
            Fragment = fragment ?? ReportFragment.CreateInfo($"{analyzerName} Results", "Content"),
            Duration = duration ?? TimeSpan.FromMilliseconds(100),
            SkipFurtherProcessing = skipFurtherProcessing
        };

        if (!success)
        {
            result.AddError("Test error");
        }

        return result;
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}