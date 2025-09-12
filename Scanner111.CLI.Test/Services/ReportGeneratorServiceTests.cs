using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Test.Services;

public class ReportGeneratorServiceTests : IDisposable
{
    private readonly ReportGeneratorService _service;
    private readonly IAdvancedReportGenerator _mockReportGenerator;
    private readonly string _testDirectory;

    public ReportGeneratorServiceTests()
    {
        _mockReportGenerator = Substitute.For<IAdvancedReportGenerator>();
        var logger = Substitute.For<ILogger<ReportGeneratorService>>();
        _service = new ReportGeneratorService(_mockReportGenerator, logger);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ReportGeneratorTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task GenerateReportAsync_WithTextFormat_GeneratesReport()
    {
        // Arrange
        var results = CreateTestResults();
        var expectedReport = "Test Report Content";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportTemplate.Predefined.Summary, null, CancellationToken.None);

        // Assert
        report.Should().Be(expectedReport);
        await _mockReportGenerator.Received(1).GenerateReportAsync(
            Arg.Is<IEnumerable<AnalysisResult>>(r => r.Count() == 2),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateReportAsync_WithHtmlFormat_GeneratesHtmlReport()
    {
        // Arrange
        var results = CreateTestResults();
        var expectedReport = "<html><body>Test Report</body></html>";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            ReportFormat.Html,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportTemplate.Predefined.Technical, null, CancellationToken.None);

        // Assert
        report.Should().Be(expectedReport);
        report.Should().Contain("<html>");
    }

    [Fact]
    public async Task GenerateReportAsync_WithJsonFormat_GeneratesJsonReport()
    {
        // Arrange
        var results = CreateTestResults();
        var expectedReport = "{\"title\":\"Test Report\",\"results\":[]}";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            ReportFormat.Json,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportTemplate.Predefined.Full, null, CancellationToken.None);

        // Assert
        report.Should().Be(expectedReport);
        report.Should().StartWith("{");
        report.Should().EndWith("}");
    }

    [Fact]
    public async Task GenerateReportAsync_WithMarkdownFormat_GeneratesMarkdownReport()
    {
        // Arrange
        var results = CreateTestResults();
        var expectedReport = "# Test Report\n\n## Results";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            ReportFormat.Markdown,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportFormat.Markdown, CancellationToken.None);

        // Assert
        report.Should().Be(expectedReport);
        report.Should().Contain("# Test Report");
    }

    [Fact]
    public async Task GenerateReportAsync_WithEmptyResults_ReturnsEmptyReport()
    {
        // Arrange
        var results = new List<AnalysisResult>();
        var expectedReport = "No analysis results";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportTemplate.Predefined.Summary, null, CancellationToken.None);

        // Assert
        report.Should().Be(expectedReport);
    }

    [Fact]
    public async Task GenerateReportAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var results = CreateTestResults();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _service.GenerateReportAsync(results, ReportFormat.PlainText, cts.Token));
    }

    [Fact]
    public async Task SaveReportAsync_CreatesFileWithContent()
    {
        // Arrange
        var results = CreateTestResults();
        var reportContent = "Test Report Content";
        var outputPath = Path.Combine(_testDirectory, "report.txt");
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(reportContent));

        // Act
        await _service.SaveReportAsync(results, outputPath, ReportFormat.PlainText, CancellationToken.None);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var savedContent = await File.ReadAllTextAsync(outputPath);
        savedContent.Should().Be(reportContent);
    }

    [Fact]
    public async Task SaveReportAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var results = CreateTestResults();
        var subDir = Path.Combine(_testDirectory, "subdir", "nested");
        var outputPath = Path.Combine(subDir, "report.txt");
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("content"));

        // Act
        await _service.SaveReportAsync(results, outputPath, ReportFormat.PlainText, CancellationToken.None);

        // Assert
        Directory.Exists(subDir).Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateReportAsync_CallsReportGeneratorWithCorrectParameters()
    {
        // Arrange
        var results = CreateTestResults();
        var expectedReport = "Generated Report";
        
        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportTemplate>(),
            Arg.Any<AdvancedReportOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _service.GenerateReportAsync(results, ReportTemplate.Predefined.Technical, null, CancellationToken.None);

        // Assert
        await _mockReportGenerator.Received(1).GenerateReportAsync(
            Arg.Is<IEnumerable<AnalysisResult>>(r => r.Count() == 2),
            ReportFormat.Html,
            Arg.Any<CancellationToken>());
    }

    private List<AnalysisResult> CreateTestResults()
    {
        var fragment1 = new ReportFragmentBuilder("Test Analysis 1")
            .WithType(ReportFragmentType.Information)
            .AppendLine("Analysis 1 content")
            .Build();

        var fragment2 = new ReportFragmentBuilder("Test Analysis 2")
            .WithType(ReportFragmentType.Warning)
            .AppendLine("Analysis 2 content")
            .Build();

        return new List<AnalysisResult>
        {
            new AnalysisResult("Analyzer1")
            {
                Success = true,
                Fragment = fragment1
            },
            new AnalysisResult("Analyzer2")
            {
                Success = true,
                Fragment = fragment2
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}