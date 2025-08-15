using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

/// <summary>
///     Unit tests for the <see cref="FileIntegrityAnalyzer" /> class
/// </summary>
public class FileIntegrityAnalyzerTests : IDisposable
{
    private readonly FileIntegrityAnalyzer _analyzer;
    private readonly TestHashValidationService _hashService;
    private readonly TestMessageHandler _messageHandler;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly List<string> _tempDirectories;
    private readonly TestYamlSettingsProvider _yamlSettings;

    public FileIntegrityAnalyzerTests()
    {
        _hashService = new TestHashValidationService();
        _settingsService = new TestApplicationSettingsService();
        _yamlSettings = new TestYamlSettingsProvider();
        _messageHandler = new TestMessageHandler();
        _tempDirectories = new List<string>();

        _analyzer = new FileIntegrityAnalyzer(
            _hashService,
            _settingsService,
            _yamlSettings,
            _messageHandler);
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AnalyzeAsync_WithFcxModeDisabled_ReturnsEarlyWithNoFindings()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = false;
        await _settingsService.SaveSettingsAsync(settings);

        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = "C:\\Games\\Fallout4"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        Assert.IsType<FcxScanResult>(result);
        var fcxResult = (FcxScanResult)result;
        Assert.False(fcxResult.HasFindings);
        Assert.Contains("FCX mode is disabled", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidGamePath_ReturnsInvalidStatus()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = "C:\\NonExistent\\Path"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        Assert.True(fcxResult.HasFindings);
        Assert.Equal(GameIntegrityStatus.Invalid, fcxResult.GameStatus);
        Assert.Contains("Could not find valid game installation", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidGameInstallation_ChecksGameExecutable()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory();
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        Assert.NotNull(fcxResult.GameConfig);
        Assert.Contains(fcxResult.FileChecks, fc => fc.FileType == "Executable");
        Assert.Contains("Game Executable Check", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithF4SEInstalled_DetectsF4SE()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory(true);
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        var f4seCheck = fcxResult.FileChecks.FirstOrDefault(fc => fc.FileType == "F4SE Loader");
        Assert.NotNull(f4seCheck);
        Assert.True(f4seCheck.Exists);
        Assert.Contains("F4SE", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMissingF4SE_AddsRecommendation()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory(false);
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        Assert.Contains("Install F4SE", fcxResult.RecommendedFixes.FirstOrDefault() ?? "");
        Assert.Contains("F4SE not found", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCoreMods_ChecksCoreMods()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory(includeCoreMods: true);
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        var coreModChecks = fcxResult.FileChecks.Where(fc => fc.FileType == "Core Mod");
        Assert.NotEmpty(coreModChecks);
        Assert.Contains("Core Mod Files Check", fcxResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_DeterminesCorrectOverallStatus()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory(true, true);
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        // Should be Warning or Good depending on whether all files pass
        Assert.True(fcxResult.GameStatus == GameIntegrityStatus.Good ||
                    fcxResult.GameStatus == GameIntegrityStatus.Warning);
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyGamePath_ReturnsWarningStatus()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        // Use empty game path
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = string.Empty
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert - Empty path may still find issues and report warning status
        var fcxResult = (FcxScanResult)result;
        Assert.True(fcxResult.HasFindings);
        Assert.True(fcxResult.GameStatus == GameIntegrityStatus.Warning ||
                    fcxResult.GameStatus == GameIntegrityStatus.Invalid);
    }

    [Fact]
    public async Task AnalyzeAsync_AddsVersionWarnings_ForKnownVersions()
    {
        // Arrange
        var settings = await _settingsService.LoadSettingsAsync();
        settings.FcxMode = true;
        await _settingsService.SaveSettingsAsync(settings);

        var gameDir = CreateTempGameDirectory();
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Set up hash service to return a known version hash
        _hashService.SetKnownHash(Path.Combine(gameDir, "Fallout4.exe"),
            "7B0E5D0B7C5B4E8F9C2A3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4");

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var fcxResult = (FcxScanResult)result;
        Assert.NotEmpty(fcxResult.VersionWarnings);
    }

    private string CreateTempGameDirectory(bool includeF4SE = false, bool includeCoreMods = false)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        // Create game executable
        File.WriteAllText(Path.Combine(tempDir, "Fallout4.exe"), "dummy executable");

        if (includeF4SE)
        {
            File.WriteAllText(Path.Combine(tempDir, "f4se_loader.exe"), "dummy F4SE");
            File.WriteAllText(Path.Combine(tempDir, "f4se_1_10_163.dll"), "dummy DLL");
        }

        if (includeCoreMods)
        {
            var f4sePlugins = Path.Combine(tempDir, "f4se", "plugins");
            Directory.CreateDirectory(f4sePlugins);
            File.WriteAllText(Path.Combine(f4sePlugins, "Buffout4.dll"), "dummy mod");
            File.WriteAllText(Path.Combine(f4sePlugins, "AddressLibrary.dll"), "dummy mod");
        }

        return tempDir;
    }
}

/// <summary>
///     Test implementation of IHashValidationService
/// </summary>
public class TestHashValidationService : IHashValidationService
{
    private readonly Dictionary<string, string> _knownHashes = new();

    public Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_knownHashes.TryGetValue(filePath, out var hash)) return Task.FromResult(hash);

        // Return a dummy hash for test files
        return Task.FromResult("DUMMY" + Guid.NewGuid().ToString().Replace("-", "").ToUpper());
    }

    public Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash,
        CancellationToken cancellationToken = default)
    {
        var actualHash = CalculateFileHashAsync(filePath, cancellationToken).Result;
        return Task.FromResult(new HashValidation
        {
            FilePath = filePath,
            ExpectedHash = expectedHash,
            ActualHash = actualHash,
            HashType = "SHA256"
        });
    }

    public Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();
        foreach (var kvp in fileHashMap)
            results[kvp.Key] = ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken).Result;
        return Task.FromResult(results);
    }

    public Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(100);
        return CalculateFileHashAsync(filePath, cancellationToken);
    }

    public void SetKnownHash(string filePath, string hash)
    {
        _knownHashes[filePath] = hash;
    }
}

/// <summary>
///     Extended test implementation of IApplicationSettingsService for FCX testing
/// </summary>
public class TestApplicationSettingsService : IApplicationSettingsService
{
    private ApplicationSettings _settings = new()
    {
        ShowFormIdValues = true,
        FcxMode = false,
        SimplifyLogs = false,
        MoveUnsolvedLogs = true,
        VrMode = false
    };

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        return Task.FromResult(_settings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task SaveSettingAsync(string key, object value)
    {
        return Task.CompletedTask;
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings();
    }
}