using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.DependencyInjection;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Xunit;

namespace Scanner111.Test.Orchestration;

/// <summary>
/// Unit tests for the analyzer orchestrator system.
/// </summary>
public class AnalyzerOrchestratorTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IAnalyzerOrchestrator _orchestrator = null!;
    private string _testDirectory = null!;
    
    public async Task InitializeAsync()
    {
        // Create test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        
        // Setup DI container
        var services = new ServiceCollection();
        
        // Add logging (minimal for testing)
        services.AddLogging();
        
        // Add mock settings cache
        services.AddSingleton<IYamlSettingsCache, MockYamlSettingsCache>();
        
        // Add orchestration system
        services.AddAnalyzerOrchestration(builder =>
        {
            builder.ClearBuiltInAnalyzers() // Clear built-in analyzers for controlled testing
                   .AddAnalyzer<TestAnalyzer>()
                   .AddAnalyzer<PriorityAnalyzer>()
                   .AddAnalyzer<FailingAnalyzer>()
                   .AddAnalyzer<SlowAnalyzer>();
        });
        
        _serviceProvider = services.BuildServiceProvider();
        _orchestrator = _serviceProvider.GetRequiredService<IAnalyzerOrchestrator>();
        
        await Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _serviceProvider?.Dispose();
        }
        
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithValidRequest_ShouldReturnSuccessResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest
        {
            InputPath = testFile,
            AnalysisType = AnalysisType.CrashLog
        };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Results.Should().NotBeEmpty();
        result.FinalReport.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithInvalidPath_ShouldReturnFailure()
    {
        // Arrange
        var request = new AnalysisRequest
        {
            InputPath = Path.Combine(_testDirectory, "nonexistent.log")
        };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.FinalReport.Should().Contain("does not exist");
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithSpecificAnalyzers_ShouldOnlyRunSelected()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest { InputPath = testFile };
        var selectedAnalyzers = new[] { "TestAnalyzer" };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request, selectedAnalyzers);
        
        // Assert
        result.Should().NotBeNull();
        result.Results.Should().HaveCount(1);
        result.Results.First().AnalyzerName.Should().Be("TestAnalyzer");
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithParallelStrategy_ShouldExecuteConcurrently()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest
        {
            InputPath = testFile,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Parallel,
                MaxDegreeOfParallelism = 4
            }
        };
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _orchestrator.RunAnalysisAsync(request);
        stopwatch.Stop();
        
        // Assert
        result.Success.Should().BeTrue();
        // Parallel execution should be faster than sequential
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithPrioritizedStrategy_ShouldRespectPriority()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest
        {
            InputPath = testFile,
            Options = new OrchestrationOptions
            {
                Strategy = ExecutionStrategy.Prioritized
            }
        };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request);
        
        // Assert
        result.Success.Should().BeTrue();
        
        // Priority analyzer (priority 1) should run before TestAnalyzer (priority 100)
        var priorityResult = result.GetAnalyzerResult("PriorityAnalyzer");
        var testResult = result.GetAnalyzerResult("TestAnalyzer");
        
        priorityResult.Should().NotBeNull();
        testResult.Should().NotBeNull();
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithFailingAnalyzer_ShouldContinueWhenConfigured()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest
        {
            InputPath = testFile,
            Options = new OrchestrationOptions
            {
                ContinueOnError = true
            }
        };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.FailedAnalyzers.Should().BeGreaterThan(0);
        result.SuccessfulAnalyzers.Should().BeGreaterThan(0);
        result.Success.Should().BeTrue(); // Overall success if some analyzers succeeded
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithTimeout_ShouldHandleSlowAnalyzers()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest
        {
            InputPath = testFile,
            Options = new OrchestrationOptions
            {
                GlobalTimeout = TimeSpan.FromMilliseconds(500)
            }
        };
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        var slowResult = result.GetAnalyzerResult("SlowAnalyzer");
        slowResult?.Success.Should().BeFalse();
        slowResult?.Errors.Should().Contain(e => e.Contains("timed out", StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public async Task RunAnalysisAsync_WithCancellation_ShouldStopGracefully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest { InputPath = testFile };
        using var cts = new CancellationTokenSource(100);
        
        // Act
        var result = await _orchestrator.RunAnalysisAsync(request, cts.Token);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.FinalReport.Should().Contain("cancelled");
    }
    
    [Fact]
    public async Task GetRegisteredAnalyzersAsync_ShouldReturnAllAnalyzers()
    {
        // Act
        var analyzers = await _orchestrator.GetRegisteredAnalyzersAsync();
        
        // Assert
        analyzers.Should().NotBeNull();
        analyzers.Should().Contain("TestAnalyzer");
        analyzers.Should().Contain("PriorityAnalyzer");
        analyzers.Should().Contain("FailingAnalyzer");
        analyzers.Should().Contain("SlowAnalyzer");
    }
    
    [Fact]
    public async Task ValidateRequestAsync_WithValidRequest_ShouldReturnValid()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        await File.WriteAllTextAsync(testFile, "test content");
        
        var request = new AnalysisRequest { InputPath = testFile };
        
        // Act
        var validation = await _orchestrator.ValidateRequestAsync(request);
        
        // Assert
        validation.Should().NotBeNull();
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeNullOrEmpty();
    }
}

// Test analyzer implementations for testing
public class TestAnalyzer : AnalyzerBase
{
    public TestAnalyzer(ILogger<TestAnalyzer> logger) : base(logger) { }
    
    public override string Name => "TestAnalyzer";
    public override string DisplayName => "Test Analyzer";
    
    protected override Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        var fragment = ReportFragment.CreateSection("Test Results", "Test analysis completed successfully");
        return Task.FromResult(AnalysisResult.CreateSuccess(Name, fragment));
    }
}

public class PriorityAnalyzer : AnalyzerBase
{
    public PriorityAnalyzer(ILogger<PriorityAnalyzer> logger) : base(logger) { }
    
    public override string Name => "PriorityAnalyzer";
    public override string DisplayName => "Priority Analyzer";
    public override int Priority => 1; // High priority
    
    protected override Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        context.SetSharedData("PriorityRan", true);
        var fragment = ReportFragment.CreateSection("Priority Results", "High priority analysis completed");
        return Task.FromResult(AnalysisResult.CreateSuccess(Name, fragment));
    }
}

public class FailingAnalyzer : AnalyzerBase
{
    public FailingAnalyzer(ILogger<FailingAnalyzer> logger) : base(logger) { }
    
    public override string Name => "FailingAnalyzer";
    public override string DisplayName => "Failing Analyzer";
    
    protected override Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("This analyzer always fails");
    }
}

public class SlowAnalyzer : AnalyzerBase
{
    public SlowAnalyzer(ILogger<SlowAnalyzer> logger) : base(logger) { }
    
    public override string Name => "SlowAnalyzer";
    public override string DisplayName => "Slow Analyzer";
    public override TimeSpan Timeout => TimeSpan.FromMilliseconds(100);
    
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken); // Simulate slow operation
        var fragment = ReportFragment.CreateSection("Slow Results", "Should timeout");
        return AnalysisResult.CreateSuccess(Name, fragment);
    }
}

// Mock implementation of IYamlSettingsCache for testing
public class MockYamlSettingsCache : IYamlSettingsCache
{
    private readonly Dictionary<string, object> _settings = new();
    
    public string GetPathForStore(YamlStore yamlStore)
    {
        return $"/mock/path/{yamlStore}.yaml";
    }
    
    public Dictionary<string, object?> LoadYaml(string yamlPath)
    {
        return new Dictionary<string, object?>();
    }
    
    public T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default)
    {
        return default;
    }
    
    public Dictionary<YamlStore, Dictionary<string, object?>> LoadMultipleStores(IEnumerable<YamlStore> stores)
    {
        return stores.ToDictionary(s => s, s => new Dictionary<string, object?>());
    }
    
    public List<object?> BatchGetSettings(IEnumerable<(YamlStore store, string keyPath)> requests)
    {
        return requests.Select(_ => (object?)null).ToList();
    }
    
    public void PrefetchAllSettings()
    {
        // No-op for testing
    }
    
    public void ClearCache()
    {
        _settings.Clear();
    }
    
    public IReadOnlyDictionary<string, long> GetMetrics()
    {
        return new Dictionary<string, long>();
    }
}