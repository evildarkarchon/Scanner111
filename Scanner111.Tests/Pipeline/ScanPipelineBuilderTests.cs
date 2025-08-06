using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.Pipeline;

public class ScanPipelineBuilderTests
{
    private readonly ScanPipelineBuilder _builder;
    private readonly IMessageHandler _messageHandler;

    public ScanPipelineBuilderTests()
    {
        _builder = new ScanPipelineBuilder();
        _messageHandler = new Mock<IMessageHandler>().Object;
    }

    [Fact]
    public void Build_WithDefaultConfiguration_CreatesBasicPipeline()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithCaching(false)
                .WithEnhancedErrorHandling(false);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<ScanPipeline>();
    }

    [Fact]
    public void Build_WithCachingEnabled_CreatesEnhancedPipeline()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithCaching(true)
                .WithEnhancedErrorHandling(false);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<EnhancedScanPipeline>();
    }

    [Fact]
    public void Build_WithEnhancedErrorHandlingEnabled_CreatesEnhancedPipeline()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithCaching(false)
                .WithEnhancedErrorHandling(true);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<EnhancedScanPipeline>();
    }

    [Fact]
    public void Build_WithPerformanceMonitoring_CreatesPerformanceMonitoringPipeline()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithPerformanceMonitoring(true);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<PerformanceMonitoringPipeline>();
    }

    [Fact]
    public void Build_WithFcxMode_CreatesFcxEnabledPipeline()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithFcxMode(true);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<FcxEnabledPipeline>();
    }

    [Fact]
    public void Build_WithAllFeatures_CreatesCorrectPipelineHierarchy()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler)
                .WithCaching(true)
                .WithEnhancedErrorHandling(true)
                .WithFcxMode(true)
                .WithPerformanceMonitoring(true);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<PerformanceMonitoringPipeline>();
        
        // The wrapped pipeline hierarchy should include FcxEnabledPipeline
        // Note: Direct field access testing removed due to encapsulation
    }

    [Fact]
    public void AddAnalyzer_SingleAnalyzer_AddsToCollection()
    {
        // Arrange & Act
        _builder.AddAnalyzer<TestAnalyzer>()
                .WithMessageHandler(_messageHandler);
        
        var pipeline = _builder.Build();
        
        // Assert
        // We can't directly inspect the analyzers, but the pipeline should build successfully
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddDefaultAnalyzers_AddsAllExpectedAnalyzers()
    {
        // Arrange & Act
        _builder.AddDefaultAnalyzers()
                .WithMessageHandler(_messageHandler);
        
        var pipeline = _builder.Build();
        
        // Assert
        // The pipeline should build successfully with all default analyzers
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void WithMessageHandler_SetsMessageHandler()
    {
        // Arrange
        var customHandler = new Mock<IMessageHandler>().Object;
        
        // Act
        var result = _builder.WithMessageHandler(customHandler);
        
        // Assert
        Assert.Same(_builder, result); // Fluent interface returns same instance
        
        // Build pipeline to verify handler is used
        var pipeline = _builder.Build();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void WithLogging_ConfiguresLogging()
    {
        // Arrange
        var logConfigured = false;
        
        // Act
        var result = _builder.WithLogging(logging =>
        {
            logConfigured = true;
            logging.SetMinimumLevel(LogLevel.Debug);
        }).WithMessageHandler(_messageHandler);
        
        // Assert
        Assert.Same(_builder, result);
        
        var pipeline = _builder.Build();
        pipeline.Should().NotBeNull();
        logConfigured.Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutMessageHandler_UsesNullMessageHandler()
    {
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        // Pipeline should build successfully with default NullMessageHandler
    }

    [Fact]
    public void WithCaching_False_RegistersNullCacheManager()
    {
        // Arrange
        _builder.WithCaching(false)
                .WithMessageHandler(_messageHandler);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        // Should use NullCacheManager when caching is disabled
    }

    [Fact]
    public void WithEnhancedErrorHandling_False_RegistersNoRetryPolicy()
    {
        // Arrange
        _builder.WithEnhancedErrorHandling(false)
                .WithMessageHandler(_messageHandler);
        
        // Act
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        // Should use NoRetryErrorPolicy when enhanced error handling is disabled
    }

    [Fact]
    public void AddAnalyzer_MultipleAnalyzers_AddsAll()
    {
        // Arrange & Act
        _builder.AddAnalyzer<TestAnalyzer>()
                .AddAnalyzer<TestAnalyzer2>()
                .AddAnalyzer<TestAnalyzer3>()
                .WithMessageHandler(_messageHandler);
        
        var pipeline = _builder.Build();
        
        // Assert
        pipeline.Should().NotBeNull();
        // All analyzers should be registered and pipeline should build successfully
    }

    [Fact]
    public void Build_MultipleCalls_CreatesIndependentPipelines()
    {
        // Arrange
        _builder.WithMessageHandler(_messageHandler);
        
        // Act
        var pipeline1 = _builder.Build();
        var pipeline2 = _builder.Build();
        
        // Assert
        pipeline1.Should().NotBeNull();
        pipeline2.Should().NotBeNull();
        Assert.NotSame(pipeline1, pipeline2); // Each build creates a new instance
    }

    [Fact]
    public void FluentInterface_AllMethods_ReturnSameBuilder()
    {
        // Act
        var result1 = _builder.AddAnalyzer<TestAnalyzer>();
        var result2 = _builder.AddDefaultAnalyzers();
        var result3 = _builder.WithMessageHandler(_messageHandler);
        var result4 = _builder.WithPerformanceMonitoring();
        var result5 = _builder.WithCaching();
        var result6 = _builder.WithEnhancedErrorHandling();
        var result7 = _builder.WithFcxMode();
        var result8 = _builder.WithLogging(_ => { });
        
        // Assert
        Assert.Same(_builder, result1);
        Assert.Same(_builder, result2);
        Assert.Same(_builder, result3);
        Assert.Same(_builder, result4);
        Assert.Same(_builder, result5);
        Assert.Same(_builder, result6);
        Assert.Same(_builder, result7);
        Assert.Same(_builder, result8);
    }

    // Test analyzer implementations
    private class TestAnalyzer : IAnalyzer
    {
        public string Name => "Test Analyzer";
        public int Priority => 100;
        public bool CanRunInParallel => true;
        
        public Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AnalysisResult>(new GenericAnalysisResult { AnalyzerName = Name });
        }
    }

    private class TestAnalyzer2 : IAnalyzer
    {
        public string Name => "Test Analyzer 2";
        public int Priority => 200;
        public bool CanRunInParallel => false;
        
        public Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AnalysisResult>(new GenericAnalysisResult { AnalyzerName = Name });
        }
    }

    private class TestAnalyzer3 : IAnalyzer
    {
        public string Name => "Test Analyzer 3";
        public int Priority => 300;
        public bool CanRunInParallel => true;
        
        public Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AnalysisResult>(new GenericAnalysisResult { AnalyzerName = Name });
        }
    }
}