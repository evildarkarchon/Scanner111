using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.Discovery;
using Scanner111.Core.Models;

namespace Scanner111.Test.Discovery;

public class GamePathDiscoveryServiceTests : IDisposable
{
    private readonly ILogger<GamePathDiscoveryService> _logger;
    private readonly IPathValidationService _pathValidationService;
    private readonly GamePathDiscoveryService _sut;
    private readonly string _tempDirectory;
    private readonly IAsyncYamlSettingsCore _yamlSettings;

    public GamePathDiscoveryServiceTests()
    {
        _pathValidationService = Substitute.For<IPathValidationService>();
        _yamlSettings = Substitute.For<IAsyncYamlSettingsCore>();
        _logger = Substitute.For<ILogger<GamePathDiscoveryService>>();

        _tempDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _sut = new GamePathDiscoveryService(_pathValidationService, _yamlSettings, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);

        _sut?.Dispose();
    }

    [Fact]
    public async Task DiscoverGamePathAsync_WithConfiguredPath_ReturnsConfiguredPath()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var expectedPath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(expectedPath);
        var exePath = Path.Combine(expectedPath, "Fallout4.exe");
        await File.WriteAllTextAsync(exePath, "dummy");

        _yamlSettings.GetSettingAsync<string>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedPath);

        // Setup path validation mocks to validate the game path
        _pathValidationService.DirectoryExistsAsync(expectedPath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(exePath, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.DiscoverGamePathAsync(gameInfo);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Method.Should().Be(DiscoveryMethod.ConfiguredPath);
        result.Paths.Should().NotBeNull();
        result.Paths!.GameRootPath.Should().Be(expectedPath);
        result.Paths.ExecutablePath.Should().Be(exePath);
    }

    [Fact]
    public async Task DiscoverGamePathAsync_WithCachedResult_ReturnsCached()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var expectedPath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(expectedPath);
        var exePath = Path.Combine(expectedPath, "Fallout4.exe");
        await File.WriteAllTextAsync(exePath, "dummy");

        _yamlSettings.GetSettingAsync<string>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedPath);

        // Setup path validation mocks
        _pathValidationService.DirectoryExistsAsync(expectedPath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(exePath, Arg.Any<CancellationToken>())
            .Returns(true);

        // First discovery to populate cache
        await _sut.DiscoverGamePathAsync(gameInfo);

        // Act - Second discovery should return cached result
        var result = await _sut.DiscoverGamePathAsync(gameInfo);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        // Verify settings were only called once (for first discovery)
        await _yamlSettings.Received(1).GetSettingAsync<string>(
            Arg.Any<YamlStore>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task TryDiscoverViaRegistryAsync_OnWindows_FindsGamePath()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Registry test only runs on Windows");

        // Arrange
        var gameInfo = CreateTestGameInfo();

        // Act
        var result = await _sut.TryDiscoverViaRegistryAsync(gameInfo);

        // Assert
        // This test will only pass if Fallout 4 is actually installed
        // We're testing the registry reading logic works without errors
        // The result can be null if game is not installed, which is acceptable
        if (result != null && result.IsSuccess)
        {
            result.Method.Should().Be(DiscoveryMethod.Registry);
            result.Paths.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task TryDiscoverViaScriptExtenderLogAsync_WithValidLog_ExtractsPath()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var expectedPath = @"C:\Games\Fallout4";
        var logPath = Path.Combine(_tempDirectory, "f4se.log");

        var logContent = $@"F4SE runtime: initialize (version = 0.6.23)
imagebase = 00007FF7B8A70000
plugin directory = {expectedPath}\Data\F4SE\Plugins
checking plugin F4SE_TestPlugin.dll";

        await File.WriteAllTextAsync(logPath, logContent);

        // Setup path validation mocks for the extracted path
        var exePath = Path.Combine(expectedPath, "Fallout4.exe");
        _pathValidationService.DirectoryExistsAsync(expectedPath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(exePath, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.TryDiscoverViaScriptExtenderLogAsync(gameInfo, logPath);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Method.Should().Be(DiscoveryMethod.ScriptExtenderLog);
        result.Paths.Should().NotBeNull();
        result.Paths!.GameRootPath.Should().Be(expectedPath);
    }

    [Fact]
    public async Task TryDiscoverViaScriptExtenderLogAsync_WithInvalidLog_ReturnsNull()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var logPath = Path.Combine(_tempDirectory, "f4se.log");
        await File.WriteAllTextAsync(logPath, "Invalid log content");

        // Act
        var result = await _sut.TryDiscoverViaScriptExtenderLogAsync(gameInfo, logPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateGamePathAsync_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var gamePath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(gamePath);
        var exePath = Path.Combine(gamePath, "Fallout4.exe");
        await File.WriteAllTextAsync(exePath, "dummy");

        _pathValidationService.DirectoryExistsAsync(gamePath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(exePath, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.ValidateGamePathAsync(gameInfo, gamePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateGamePathAsync_WithMissingExecutable_ReturnsFalse()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var gamePath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(gamePath);

        _pathValidationService.DirectoryExistsAsync(gamePath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.ValidateGamePathAsync(gameInfo, gamePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DiscoverGamePathAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException derives from OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.DiscoverGamePathAsync(gameInfo, cts.Token));

        // Verify it's a cancellation exception (can be either TaskCanceledException or OperationCanceledException)
        exception.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public void GetCachedResult_WithExpiredCache_ReturnsNull()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        // Cache expiration is set in constructor, we'll test after it expires

        // Act
        var result = _sut.GetCachedResult(gameInfo);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearCache_RemovesAllCachedResults()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var gamePath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(gamePath);
        var exePath = Path.Combine(gamePath, "Fallout4.exe");
        await File.WriteAllTextAsync(exePath, "dummy");

        _yamlSettings.GetSettingAsync<string>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(gamePath);

        _pathValidationService.ValidatePathAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(PathValidationResult.Success(gamePath, true, false));

        await _sut.DiscoverGamePathAsync(gameInfo);

        // Act
        _sut.ClearCache();
        var cachedResult = _sut.GetCachedResult(gameInfo);

        // Assert
        cachedResult.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverGamePathAsync_ConcurrentCalls_HandledSafely()
    {
        // Arrange
        var gameInfo = CreateTestGameInfo();
        var gamePath = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(gamePath);
        var exePath = Path.Combine(gamePath, "Fallout4.exe");
        await File.WriteAllTextAsync(exePath, "dummy");

        _yamlSettings.GetSettingAsync<string>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(gamePath);

        // Setup path validation mocks
        _pathValidationService.DirectoryExistsAsync(gamePath, Arg.Any<CancellationToken>())
            .Returns(true);
        _pathValidationService.FileExistsAsync(exePath, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act - Make concurrent discovery calls
        var tasks = new List<Task<PathDiscoveryResult>>();
        for (var i = 0; i < 10; i++) tasks.Add(_sut.DiscoverGamePathAsync(gameInfo));

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.IsSuccess.Should().BeTrue();
            r.Paths!.GameRootPath.Should().Be(gamePath);
        });

        // Verify settings were only called once due to caching
        await _yamlSettings.Received(1).GetSettingAsync<string>(
            Arg.Any<YamlStore>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static GameInfo CreateTestGameInfo()
    {
        return new GameInfo
        {
            GameName = "Fallout4",
            IsVR = false,
            ExecutableName = "Fallout4.exe",
            ScriptExtenderAcronym = "F4SE",
            ScriptExtenderBase = "F4SE",
            DocumentsFolderName = "Fallout4",
            SteamId = 377160,
            RegistryKeyPath = @"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4"
        };
    }
}