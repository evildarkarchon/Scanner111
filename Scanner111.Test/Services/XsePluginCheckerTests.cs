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

public sealed class XsePluginCheckerTests : IDisposable
{
    private readonly ILogger<XsePluginChecker> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly XsePluginChecker _checker;
    private readonly string _tempDirectory;

    public XsePluginCheckerTests()
    {
        _logger = Substitute.For<ILogger<XsePluginChecker>>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _checker = new XsePluginChecker(_logger, _yamlCore);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"XsePluginCheckerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CheckXsePluginsAsync_WithValidConfiguration_ReturnsReport()
    {
        // Act
        var result = await _checker.CheckXsePluginsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // The method should complete without throwing exceptions
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithNonExistentDirectory_ReturnsFalseAndErrorMessage()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");
        var gameVersion = "1.10.163.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(nonExistentPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeFalse();
        message.Should().Contain("Could not locate plugins folder");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithCorrectNonVRVersion_ReturnsTrueAndSuccessMessage()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_correct_nonvr");
        Directory.CreateDirectory(pluginsPath);

        // Create correct Address Library file for non-VR
        var correctFile = Path.Combine(pluginsPath, "version-1-10-163-0.bin");
        await File.WriteAllTextAsync(correctFile, "mock address library");

        var gameVersion = "1.10.163.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeTrue();
        message.Should().Contain("✔️");
        message.Should().Contain("correct version");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithCorrectVRVersion_ReturnsTrueAndSuccessMessage()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_correct_vr");
        Directory.CreateDirectory(pluginsPath);

        // Create correct Address Library file for VR
        var correctFile = Path.Combine(pluginsPath, "version-1-2-72-0.csv");
        await File.WriteAllTextAsync(correctFile, "mock vr address library");

        var gameVersion = "1.2.72.0";
        var isVrMode = true;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeTrue();
        message.Should().Contain("✔️");
        message.Should().Contain("correct version");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithWrongVersionForNonVR_ReturnsFalseAndWarningMessage()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_wrong_nonvr");
        Directory.CreateDirectory(pluginsPath);

        // Create VR Address Library file when non-VR is expected
        var wrongFile = Path.Combine(pluginsPath, "version-1-2-72-0.csv");
        await File.WriteAllTextAsync(wrongFile, "mock vr address library");

        var gameVersion = "1.10.163.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeFalse();
        message.Should().Contain("❌");
        message.Should().Contain("wrong version");
        message.Should().Contain("Non-VR");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithWrongVersionForVR_ReturnsFalseAndWarningMessage()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_wrong_vr");
        Directory.CreateDirectory(pluginsPath);

        // Create non-VR Address Library file when VR is expected
        var wrongFile = Path.Combine(pluginsPath, "version-1-10-163-0.bin");
        await File.WriteAllTextAsync(wrongFile, "mock nonvr address library");

        var gameVersion = "1.2.72.0";
        var isVrMode = true;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeFalse();
        message.Should().Contain("❌");
        message.Should().Contain("wrong version");
        message.Should().Contain("Virtual Reality");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithNoAddressLibrary_ReturnsFalseAndNoticeMessage()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_empty");
        Directory.CreateDirectory(pluginsPath);

        var gameVersion = "1.10.163.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeFalse();
        message.Should().Contain("❓");
        message.Should().Contain("not found");
        message.Should().Contain("Non-VR");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithNewGameVersion_AcceptsNewGameAddressLibrary()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_ng");
        Directory.CreateDirectory(pluginsPath);

        // Create New Game Address Library file
        var ngFile = Path.Combine(pluginsPath, "version-1-10-984-0.bin");
        await File.WriteAllTextAsync(ngFile, "mock ng address library");

        var gameVersion = "1.10.984.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeTrue();
        message.Should().Contain("✔️");
        message.Should().Contain("correct version");
    }

    [Fact]
    public async Task CheckXsePluginsAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => _checker.CheckXsePluginsAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "cancellation_test");
        Directory.CreateDirectory(pluginsPath);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => _checker.ValidateAddressLibraryAsync(pluginsPath, "1.10.163.0", false, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ValidateAddressLibraryAsync_WithInvalidPluginsPath_ThrowsArgumentException(string? invalidPath)
    {
        // Act & Assert
        var act = () => _checker.ValidateAddressLibraryAsync(invalidPath!, "1.10.163.0", false);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ValidateAddressLibraryAsync_WithInvalidGameVersion_ThrowsArgumentException(string? invalidVersion)
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "test_plugins");
        Directory.CreateDirectory(pluginsPath);

        // Act & Assert
        var act = () => _checker.ValidateAddressLibraryAsync(pluginsPath, invalidVersion!, false);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new XsePluginChecker(null!, _yamlCore);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullYamlCore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new XsePluginChecker(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Fact]
    public async Task ValidateAddressLibraryAsync_WithMultipleCorrectVersionsForNonVR_DetectsAnyCorrectVersion()
    {
        // Arrange
        var pluginsPath = Path.Combine(_tempDirectory, "plugins_multiple_correct");
        Directory.CreateDirectory(pluginsPath);

        // Create both OG and NG versions (both valid for non-VR)
        var ogFile = Path.Combine(pluginsPath, "version-1-10-163-0.bin");
        var ngFile = Path.Combine(pluginsPath, "version-1-10-984-0.bin");
        await File.WriteAllTextAsync(ogFile, "mock og address library");
        await File.WriteAllTextAsync(ngFile, "mock ng address library");

        var gameVersion = "1.10.163.0";
        var isVrMode = false;

        // Act
        var (isCorrectVersion, message) = await _checker.ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode);

        // Assert
        isCorrectVersion.Should().BeTrue();
        message.Should().Contain("✔️");
        message.Should().Contain("correct version");
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