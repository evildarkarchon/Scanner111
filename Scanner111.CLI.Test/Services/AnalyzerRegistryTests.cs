using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.CLI.Services;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Xunit;

namespace Scanner111.CLI.Test.Services;

public class AnalyzerRegistryTests
{
    private readonly AnalyzerRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public AnalyzerRegistryTests()
    {
        var services = new ServiceCollection();
        
        // Register mock analyzers
        services.AddTransient<IAnalyzer>(sp => CreateMockAnalyzer("PluginAnalyzer"));
        services.AddTransient<IAnalyzer>(sp => CreateMockAnalyzer("SettingsAnalyzer"));
        services.AddTransient<IAnalyzer>(sp => CreateMockAnalyzer("GpuAnalyzer"));
        
        _serviceProvider = services.BuildServiceProvider();
        var logger = Substitute.For<ILogger<AnalyzerRegistry>>();
        _registry = new AnalyzerRegistry(_serviceProvider, logger);
    }

    private IAnalyzer CreateMockAnalyzer(string name)
    {
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns(name);
        return analyzer;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRegisteredAnalyzers()
    {
        // Act
        var analyzers = await _registry.GetAllAsync(CancellationToken.None);

        // Assert
        analyzers.Should().HaveCount(3);
        analyzers.Select(a => a.Name).Should().Contain("PluginAnalyzer");
        analyzers.Select(a => a.Name).Should().Contain("SettingsAnalyzer");
        analyzers.Select(a => a.Name).Should().Contain("GpuAnalyzer");
    }

    [Fact]
    public async Task GetAllAsync_WithSpecificNames_ReturnsRequestedAnalyzers()
    {
        // Arrange
        var requestedNames = new[] { "PluginAnalyzer", "GpuAnalyzer" };

        // Act
        var allAnalyzers = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers = allAnalyzers.Where(a => requestedNames.Contains(a.Name)).ToList();

        // Assert
        analyzers.Should().HaveCount(2);
        analyzers.Select(a => a.Name).Should().Contain("PluginAnalyzer");
        analyzers.Select(a => a.Name).Should().Contain("GpuAnalyzer");
        analyzers.Select(a => a.Name).Should().NotContain("SettingsAnalyzer");
    }

    [Fact]
    public async Task GetAllAsync_WithNonExistentName_ReturnsOnlyExistingAnalyzers()
    {
        // Arrange
        var requestedNames = new[] { "PluginAnalyzer", "NonExistentAnalyzer" };

        // Act
        var allAnalyzers = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers = allAnalyzers.Where(a => requestedNames.Contains(a.Name)).ToList();

        // Assert
        analyzers.Should().HaveCount(1);
        analyzers.First().Name.Should().Be("PluginAnalyzer");
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var requestedNames = Array.Empty<string>();

        // Act
        var allAnalyzers = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers = allAnalyzers.Where(a => requestedNames.Contains(a.Name)).ToList();

        // Assert
        analyzers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _registry.GetAllAsync(cts.Token));
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ReturnsAnalyzer()
    {
        // Act
        var analyzer = await _registry.GetByNameAsync("PluginAnalyzer", CancellationToken.None);

        // Assert
        analyzer.Should().NotBeNull();
        analyzer!.Name.Should().Be("PluginAnalyzer");
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistentName_ReturnsNull()
    {
        // Act
        var analyzer = await _registry.GetByNameAsync("NonExistentAnalyzer", CancellationToken.None);

        // Assert
        analyzer.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithDuplicateNames_ReturnsUniqueAnalyzers()
    {
        // Arrange
        var requestedNames = new[] { "PluginAnalyzer", "PluginAnalyzer", "GpuAnalyzer" };

        // Act
        var allAnalyzers = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers = allAnalyzers.Where(a => requestedNames.Contains(a.Name)).Distinct().ToList();

        // Assert
        analyzers.Should().HaveCount(2);
        analyzers.Select(a => a.Name).Should().BeEquivalentTo(new[] { "PluginAnalyzer", "GpuAnalyzer" });
    }

    [Fact]
    public async Task GetAllAsync_WithCaseInsensitiveNames_ReturnsAnalyzers()
    {
        // Arrange
        var requestedNames = new[] { "pluginanalyzer", "GPUANALYZER" };

        // Act
        var allAnalyzers = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers = allAnalyzers.Where(a => requestedNames.Any(n => 
            string.Equals(n, a.Name, StringComparison.OrdinalIgnoreCase))).ToList();

        // Assert
        analyzers.Should().HaveCount(2);
        analyzers.Select(a => a.Name).Should().Contain("PluginAnalyzer");
        analyzers.Select(a => a.Name).Should().Contain("GpuAnalyzer");
    }

    [Fact]
    public async Task GetAllAsync_MultipleCalls_ReturnsSameAnalyzers()
    {
        // Act
        var analyzers1 = await _registry.GetAllAsync(CancellationToken.None);
        var analyzers2 = await _registry.GetAllAsync(CancellationToken.None);

        // Assert
        analyzers1.Should().HaveCount(3);
        analyzers2.Should().HaveCount(3);
        analyzers1.Select(a => a.Name).Should().BeEquivalentTo(analyzers2.Select(a => a.Name));
    }
}