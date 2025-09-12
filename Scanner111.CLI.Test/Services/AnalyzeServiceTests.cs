using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.CLI.Configuration;
using Scanner111.CLI.Services;
using Scanner111.CLI.Test.Infrastructure;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Spectre.Console;
using Xunit;

namespace Scanner111.CLI.Test.Services;

public class AnalyzeServiceTests : CliTestBase
{
    private readonly IAnalyzerOrchestrator _mockOrchestrator;
    private readonly IAnalyzerRegistry _mockRegistry;
    private readonly IReportGeneratorService _mockReportGenerator;
    private readonly IAsyncYamlSettingsCore _mockYamlCore;
    private readonly ICliSettings _mockSettings;
    private readonly AnalyzeService _analyzeService;

    public AnalyzeServiceTests()
    {
        _mockOrchestrator = Substitute.For<IAnalyzerOrchestrator>();
        _mockRegistry = Substitute.For<IAnalyzerRegistry>();
        _mockReportGenerator = Substitute.For<IReportGeneratorService>();
        _mockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _mockSettings = Substitute.For<ICliSettings>();

        var logger = Substitute.For<ILogger<AnalyzeService>>();
        _analyzeService = new AnalyzeService(
            _mockOrchestrator,
            _mockRegistry,
            _mockReportGenerator,
            _mockSettings,
            _mockYamlCore,
            Console as IAnsiConsole,
            logger
        );
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithValidFile_ReturnsSuccessResult()
    {
        // Arrange
        var testFile = "test.log";
        var analyzers = new[] { "PluginAnalyzer", "SettingsAnalyzer" };
        var outputFile = "output.html";
        var format = "html";

        var mockAnalyzers = new List<IAnalyzer>
        {
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>()
        };

        _mockRegistry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockAnalyzers.AsEnumerable()));

        var testFragment = CreateTestReportFragment("Test Analysis");
        var orchestrationResult = new AnalysisResult("TestAnalyzer")
        {
            Success = true,
            Fragment = testFragment
        };

        var orchestrationResultObj = new OrchestrationResult();
        orchestrationResultObj.AddResult(orchestrationResult);
        
        _mockOrchestrator.RunAnalysisAsync(
            Arg.Any<AnalysisRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(orchestrationResultObj));

        _mockReportGenerator.GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(),
            Arg.Any<ReportFormat>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("Test Report"));

        // Act
        await _analyzeService.AnalyzeFileAsync(
            testFile, analyzers, outputFile, ReportFormat.Html, CancellationTokenSource.Token);

        // Assert
        await _mockOrchestrator.Received(1).RunAnalysisAsync(
            Arg.Is<AnalysisRequest>(req => req.InputPath == testFile),
            Arg.Any<CancellationToken>());
        
        await _mockReportGenerator.Received(1).GenerateReportAsync(
            Arg.Any<IEnumerable<AnalysisResult>>(), 
            ReportFormat.Html,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".log");
        var analyzers = new[] { "PluginAnalyzer" };

        // Act
        var result = await _analyzeService.AnalyzeFileAsync(
            nonExistentFile, analyzers, null, ReportFormat.Markdown, CancellationTokenSource.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var testFile = "test.log";
        var analyzers = new[] { "PluginAnalyzer" };
        CancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _analyzeService.AnalyzeFileAsync(
                testFile, analyzers, null, "text", false, CancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetAllAsync_WithSpecificAnalyzers_ReturnsRequestedAnalyzers()
    {
        // Arrange
        var requestedAnalyzers = new[] { "PluginAnalyzer", "SettingsAnalyzer" };
        var mockAnalyzers = new List<IAnalyzer>
        {
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>()
        };

        _mockRegistry.GetAllAsync(requestedAnalyzers, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockAnalyzers));

        // Act
        var result = await _analyzeService.GetAllAsync(requestedAnalyzers, CancellationTokenSource.Token);

        // Assert
        result.Should().HaveCount(2);
        await _mockRegistry.Received(1).GetAllAsync(requestedAnalyzers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_WithAllAnalyzers_ReturnsAllRegisteredAnalyzers()
    {
        // Arrange
        var requestedAnalyzers = new[] { "all" };
        var mockAnalyzers = new List<IAnalyzer>
        {
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>()
        };

        _mockRegistry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockAnalyzers.AsEnumerable()));

        // Act
        var result = await _analyzeService.GetAllAsync(requestedAnalyzers, CancellationTokenSource.Token);

        // Assert
        result.Should().HaveCount(3);
        await _mockRegistry.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyRequest_ReturnsAllAnalyzers()
    {
        // Arrange
        var mockAnalyzers = new List<IAnalyzer>
        {
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>(),
            Substitute.For<IAnalyzer>()
        };

        _mockRegistry.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockAnalyzers.AsEnumerable()));

        // Act
        var result = await _analyzeService.GetAllAsync(null, CancellationTokenSource.Token);

        // Assert
        result.Should().HaveCount(3);
        await _mockRegistry.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }




    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddSingleton(_mockOrchestrator);
        services.AddSingleton(_mockRegistry);
        services.AddSingleton(_mockReportGenerator);
        services.AddSingleton(_mockYamlCore);
        services.AddSingleton(_mockSettings);
    }
}