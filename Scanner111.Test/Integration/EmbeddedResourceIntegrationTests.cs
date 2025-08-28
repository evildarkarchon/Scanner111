using FluentAssertions;
using Microsoft.Extensions.Logging;
using Scanner111.Test.Infrastructure.TestData;
using Xunit.Abstractions;

namespace Scanner111.Test.Integration;

/// <summary>
///     Integration tests to verify embedded resources are working properly.
///     Part of Q3: Sample Removal phase migration.
/// </summary>
public class EmbeddedResourceIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly EmbeddedResourceProvider _provider;

    public EmbeddedResourceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _provider = new EmbeddedResourceProvider(loggerFactory.CreateLogger<EmbeddedResourceProvider>());
    }

    [Fact]
    public Task EmbeddedResourceProvider_GetAvailableEmbeddedLogs_ReturnsEmbeddedLogs()
    {
        // Act
        var logs = _provider.GetAvailableEmbeddedLogs().ToList();
        
        // Assert
        logs.Should().NotBeEmpty("Embedded logs should be available");
        
        // Verify we have our critical samples
        logs.Should().Contain("crash-2022-06-05-12-52-17.log");
        logs.Should().Contain("crash-2022-06-09-07-25-03.log");
        logs.Should().Contain("crash-2022-06-12-07-11-38.log");
        logs.Should().Contain("crash-2022-06-15-10-02-51.log");
        
        _output.WriteLine($"Found {logs.Count} embedded logs:");
        foreach (var log in logs)
        {
            _output.WriteLine($"  - {log}");
        }
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EmbeddedResourceProvider_GetEmbeddedLogAsync_ReturnsLogContent()
    {
        // Arrange
        const string logName = "crash-2022-06-05-12-52-17.log";
        
        // Act
        var content = await _provider.GetEmbeddedLogAsync(logName);
        
        // Assert
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Unhandled exception", "Crash logs should contain exception information");
        content.Should().Contain("Buffout 4", "These are Buffout 4 crash logs");
        
        _output.WriteLine($"Successfully loaded {logName}:");
        _output.WriteLine($"  Content length: {content.Length} bytes");
        _output.WriteLine($"  First line: {content.Split('\n').FirstOrDefault()}");
    }

    [Theory]
    [InlineData("crash-2022-06-05-12-52-17.log")]
    [InlineData("crash-2022-06-09-07-25-03.log")]
    [InlineData("crash-2022-06-12-07-11-38.log")]
    [InlineData("crash-2022-06-15-10-02-51.log")]
    public async Task EmbeddedResourceProvider_AllCriticalLogs_AreAccessible(string logName)
    {
        // Act & Assert
        var content = await _provider.GetEmbeddedLogAsync(logName);
        
        content.Should().NotBeNullOrEmpty($"{logName} should have content");
        content.Length.Should().BeGreaterThan(100, $"{logName} should have substantial content");
    }

    [Fact]
    public async Task EmbeddedResourceProvider_PreloadAllResourcesAsync_LoadsAllResources()
    {
        // Act
        await _provider.PreloadAllResourcesAsync();
        
        // Assert - verify all resources are cached
        var logs = _provider.GetAvailableEmbeddedLogs().ToList();
        foreach (var log in logs)
        {
            // This should be fast since they're cached
            var content = await _provider.GetEmbeddedLogAsync(log);
            content.Should().NotBeNullOrEmpty();
        }
        
        _output.WriteLine($"Successfully preloaded {logs.Count} embedded resources");
    }

    [Fact]
    public async Task EmbeddedResourceProvider_WriteToTempFileAsync_CreatesFile()
    {
        // Arrange
        const string logName = "crash-2022-06-05-12-52-17.log";
        var tempDir = Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // Act
            var tempPath = await _provider.WriteToTempFileAsync(logName, tempDir);
            
            // Assert
            File.Exists(tempPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempPath);
            content.Should().NotBeNullOrEmpty();
            content.Should().Contain("Unhandled exception");
            
            _output.WriteLine($"Created temp file: {tempPath}");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task EmbeddedResourceProvider_InvalidResourceName_ThrowsException()
    {
        // Arrange
        const string invalidName = "non-existent-log.log";
        
        // Act & Assert
        var act = async () => await _provider.GetEmbeddedLogAsync(invalidName);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{invalidName}*not found*");
    }

    [Fact]
    public async Task EmbeddedResourceProvider_ClearCacheAsync_ClearsCache()
    {
        // Arrange - preload some resources
        await _provider.GetEmbeddedLogAsync("crash-2022-06-05-12-52-17.log");
        
        // Act
        await _provider.ClearCacheAsync();
        
        // Assert - resource should still be accessible but will be reloaded
        var content = await _provider.GetEmbeddedLogAsync("crash-2022-06-05-12-52-17.log");
        content.Should().NotBeNullOrEmpty();
        
        _output.WriteLine("Cache cleared and resource successfully reloaded");
    }
}