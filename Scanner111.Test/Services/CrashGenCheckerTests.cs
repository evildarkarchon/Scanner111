using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Test.Services;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Service")]
public sealed class CrashGenCheckerTests : IDisposable
{
    private readonly ILogger<CrashGenChecker> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly CrashGenChecker _checker;
    private readonly string _tempDirectory;

    public CrashGenCheckerTests()
    {
        _logger = Substitute.For<ILogger<CrashGenChecker>>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _checker = new CrashGenChecker(_logger, _yamlCore);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"CrashGenCheckerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CheckCrashGenSettingsAsync_WithValidConfiguration_ReturnsSuccessReport()
    {
        // Act
        var result = await _checker.CheckCrashGenSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // The method should complete without throwing exceptions
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithEmptyDirectory_ReturnsEmptySet()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "empty_plugins");
        Directory.CreateDirectory(pluginsPath);

        // Act
        var result = await _checker.DetectInstalledPluginsAsync(pluginsPath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithPluginFiles_ReturnsPluginNames()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_with_files");
        Directory.CreateDirectory(pluginsPath);

        // Create mock plugin files
        var plugin1 = Path.Combine(pluginsPath, "achievements.dll");
        var plugin2 = Path.Combine(pluginsPath, "x-cell-fo4.dll");
        var plugin3 = Path.Combine(pluginsPath, "buffout4.dll");

        await File.WriteAllTextAsync(plugin1, "mock plugin 1");
        await File.WriteAllTextAsync(plugin2, "mock plugin 2");
        await File.WriteAllTextAsync(plugin3, "mock plugin 3");

        // Act
        var result = await _checker.DetectInstalledPluginsAsync(pluginsPath);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("achievements.dll");
        result.Should().Contain("x-cell-fo4.dll");
        result.Should().Contain("buffout4.dll");
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithNonExistentDirectory_ReturnsEmptySet()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _checker.DetectInstalledPluginsAsync(nonExistentPath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithSameDirectory_UsesCachedResult()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "cached_plugins");
        Directory.CreateDirectory(pluginsPath);

        var plugin = Path.Combine(pluginsPath, "test.dll");
        await File.WriteAllTextAsync(plugin, "mock plugin");

        // Act
        var result1 = await _checker.DetectInstalledPluginsAsync(pluginsPath);
        var result2 = await _checker.DetectInstalledPluginsAsync(pluginsPath);

        // Assert
        result1.Should().BeEquivalentTo(result2);
        result1.Should().HaveCount(1);
        result1.Should().Contain("test.dll");

        // Verify that the cache was used (both results should be the same reference if cached properly)
        ReferenceEquals(result1, result2).Should().BeTrue();
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_IsCaseInsensitive()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "case_test_plugins");
        Directory.CreateDirectory(pluginsPath);

        var plugin = Path.Combine(pluginsPath, "TestPlugin.DLL");
        await File.WriteAllTextAsync(plugin, "mock plugin");

        // Act
        var result = await _checker.DetectInstalledPluginsAsync(pluginsPath);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("testplugin.dll"); // Should be lowercase
    }

    [Fact]
    public async Task HasPluginAsync_WithInstalledPlugin_ReturnsTrue()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "test_plugins");
        Directory.CreateDirectory(pluginsPath);

        var plugin = Path.Combine(pluginsPath, "achievements.dll");
        await File.WriteAllTextAsync(plugin, "mock plugin");

        var pluginNames = new[] { "achievements.dll", "missing.dll" };

        // Act
        var result = await _checker.HasPluginAsync(pluginNames);

        // Assert
        result.Should().BeTrue(); // Should return true because achievements.dll exists
    }

    [Fact]
    public async Task HasPluginAsync_WithNoInstalledPlugins_ReturnsFalse()
    {
        // Arrange
        var pluginNames = new[] { "missing1.dll", "missing2.dll" };

        // Act  
        var result = await _checker.HasPluginAsync(pluginNames);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPluginAsync_WithEmptyPluginList_ReturnsFalse()
    {
        // Arrange
        var pluginNames = Array.Empty<string>();

        // Act
        var result = await _checker.HasPluginAsync(pluginNames);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCrashGenSettingsAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => _checker.CheckCrashGenSettingsAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "cancellation_test");
        Directory.CreateDirectory(pluginsPath);
        
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => _checker.DetectInstalledPluginsAsync(pluginsPath, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task DetectInstalledPluginsAsync_WithInvalidPath_ThrowsArgumentException(string? invalidPath)
    {
        // Act & Assert
        var act = () => _checker.DetectInstalledPluginsAsync(invalidPath!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CrashGenChecker(null!, _yamlCore);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullYamlCore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CrashGenChecker(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Fact]
    public async Task HasPluginAsync_WithNullPluginNames_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _checker.HasPluginAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectInstalledPluginsAsync_WithSubdirectories_OnlyScansTopLevel()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_with_subdirs");
        Directory.CreateDirectory(pluginsPath);

        // Create plugin in root
        var rootPlugin = Path.Combine(pluginsPath, "root.dll");
        await File.WriteAllTextAsync(rootPlugin, "root plugin");

        // Create subdirectory with plugin
        var subDir = Path.Combine(pluginsPath, "subdir");
        Directory.CreateDirectory(subDir);
        var subPlugin = Path.Combine(subDir, "sub.dll");
        await File.WriteAllTextAsync(subPlugin, "sub plugin");

        // Act
        var result = await _checker.DetectInstalledPluginsAsync(pluginsPath);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("root.dll");
        result.Should().NotContain("sub.dll"); // Should not find plugins in subdirectories
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch (Exception ex)
            {
                // Log but don't fail test cleanup
                Console.WriteLine($"Failed to cleanup temp directory: {ex.Message}");
            }
        }
    }
}