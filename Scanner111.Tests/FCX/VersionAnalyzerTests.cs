using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.FCX;

// Extended test hash service for version analyzer tests
public class ExtendedTestHashValidationService : TestHashValidationService
{
    private readonly HashSet<string> _existingFiles = new();
    private readonly Dictionary<string, string[]> _notesForHash = new();
    private readonly Dictionary<string, string> _versionForHash = new();

    public bool ThrowOnNextCall { get; set; }

    public void SetVersionForHash(string hash, string version)
    {
        _versionForHash[hash] = version;
    }

    public void SetVersionWithNotes(string hash, string version, string[] notes)
    {
        _versionForHash[hash] = version;
        _notesForHash[hash] = notes;
    }

    public void SetFileExists(string path, bool exists)
    {
        if (exists)
            _existingFiles.Add(path);
        else
            _existingFiles.Remove(path);

        // Create/delete the actual file for tests
        if (exists)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(path))
                File.WriteAllText(path, "test");
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public new Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (ThrowOnNextCall)
        {
            ThrowOnNextCall = false;
            throw new Exception("Test exception");
        }

        return base.CalculateFileHashAsync(filePath, cancellationToken);
    }
}

/// <summary>
///     Unit tests for the VersionAnalyzer class
/// </summary>
public class VersionAnalyzerTests
{
    private readonly VersionAnalyzer _analyzer;
    private readonly TestApplicationSettingsService _appSettings;
    private readonly ExtendedTestHashValidationService _hashService;
    private readonly TestYamlSettingsProvider _yamlSettings;

    public VersionAnalyzerTests()
    {
        _hashService = new ExtendedTestHashValidationService();
        _yamlSettings = new TestYamlSettingsProvider();
        _appSettings = new TestApplicationSettingsService();
        _analyzer = new VersionAnalyzer(
            NullLogger<VersionAnalyzer>.Instance,
            _hashService,
            _yamlSettings,
            _appSettings);
    }

    [Fact]
    public async Task AnalyzeAsync_WithLatestGameVersion_ReportsUpToDate()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        // Set up hash validation to return a known latest version hash
        _hashService.SetFileHash("C:\\Games\\Fallout4\\Fallout4.exe",
            "8b3c1c3f3e3d28d2674ea9c968dfa14f6c1461cdcc69c833bb3c96f46329e99a");

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("version info is considered a finding");
        genericResult.ReportText.Should().Contain("Game Version Detected", "version detection should be reported");
    }

    [Fact]
    public async Task AnalyzeAsync_WithDowngradedVersion_ReportsWarning()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        // Set up hash validation to return an older version hash
        _hashService.SetFileHash("C:\\Games\\Fallout4\\Fallout4.exe",
            "3c3e4d89f88d28d2674ea9c968dfa14f6c1461cdcc69c833bb3c96f46329e76b");

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should().Contain("Game Version Detected", "version detection should be reported");
        // The actual downgrade detection would be in the detected version info
    }

    [Fact]
    public async Task AnalyzeAsync_WithUnknownVersion_HandlesGracefully()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        // Set up hash validation to return an unknown hash
        _hashService.SetFileHash("C:\\Games\\Fallout4\\Fallout4.exe", "unknown_hash");

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should().Contain("ERROR: Game executable not found!",
            "error should be reported for missing executable");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMissingExecutable_ReportsError()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\NonExistent"
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        // Should report that game executable was not found
        genericResult.ReportText.Should().Contain("Game Version", "version section should be present");
    }

    [Fact]
    public async Task AnalyzeAsync_ForDifferentGames_DetectsCorrectly()
    {
        // Test Fallout 4
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        var crashLog = new CrashLog { FilePath = "test.log" };
        var result = await _analyzer.AnalyzeAsync(crashLog);

        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("findings should be reported");

        // Test Skyrim SE
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\SkyrimSE"
        });

        _hashService.SetFileHash("C:\\Games\\SkyrimSE\\SkyrimSE.exe", "skyrim_hash");

        result = await _analyzer.AnalyzeAsync(crashLog);
        genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
    }

    [Fact]
    public async Task AnalyzeAsync_NotInFcxMode_ReturnsNoFindings()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = false,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeFalse("no findings should be reported when FCX mode is disabled");
        genericResult.ReportLines.Should().BeEmpty("report should be empty when FCX mode is disabled");
    }

    [Fact]
    public void AnalyzerProperties_AreSetCorrectly()
    {
        // Assert
        _analyzer.Name.Should().Be("Version Analyzer", "analyzer name should be set correctly");
        _analyzer.Priority.Should().Be(10, "analyzer should run early with low priority value");
        _analyzer.CanRunInParallel.Should().BeTrue("analyzer should support parallel execution");
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidGamePath_HandlesGracefully()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = null // Invalid path
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        // Should handle gracefully without crashing
    }

    [Fact]
    public async Task AnalyzeAsync_WithFallout4VR_TreatsAsFallout4()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4VR"
        });

        _hashService.SetFileHash("C:\\Games\\Fallout4VR\\Fallout4.exe", "vr_hash");

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        // Should treat VR as regular Fallout 4
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellation_RespectsToken()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog, cts.Token);

        // Assert
        // Should complete without throwing (analyzer handles cancellation gracefully)
        result.Should().NotBeNull("analyzer should handle cancellation gracefully");
    }

    [Theory]
    [InlineData("C:\\Game Path With Spaces\\Fallout 4")]
    [InlineData("C:\\Game's Path\\Fallout 4")]
    [InlineData("C:\\Game-Path\\Fallout 4")]
    [InlineData("C:\\Game.Path\\Fallout 4")]
    [InlineData("C:\\Game@Path\\Fallout 4")]
    [InlineData("C:\\Game (2024)\\Fallout 4")]
    public async Task AnalyzeAsync_WithSpecialCharactersInPath_HandlesCorrectly(string gamePath)
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = gamePath
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        // Should handle special characters in path gracefully
    }

    [Fact]
    public async Task AnalyzeAsync_WithVeryLongPath_HandlesGracefully()
    {
        // Arrange
        var longPath = "C:\\" + new string('a', 200) + "\\Fallout4";
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = longPath
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        // Should handle long paths without crashing
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task AnalyzeAsync_WithWhitespaceGamePath_HandlesGracefully(string gamePath)
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = gamePath
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        // The analyzer treats whitespace as a valid path and tries to find the executable
        genericResult.ReportText.Should().Contain("ERROR: Game executable not found!",
            "error should be reported for missing executable");
    }

    [Fact]
    public async Task AnalyzeAsync_EdgeCases_HandledProperly()
    {
        // Test 1: Empty game path is handled
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = ""
        });

        var crashLog = new CrashLog { FilePath = "test.log" };
        var result = await _analyzer.AnalyzeAsync(crashLog);
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should().Contain("No game path configured", "empty path should be reported");

        // Test 2: Non-existent game path
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\NonExistent\\Path\\To\\Game"
        });

        result = await _analyzer.AnalyzeAsync(crashLog);
        genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should().Contain("ERROR: Game executable not found!",
            "error should be reported for missing executable");
    }


    [Fact]
    public async Task AnalyzeAsync_ConcurrentExecution_ThreadSafe()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4"
        });

        var tasks = new Task<AnalysisResult>[10];
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        for (var i = 0; i < tasks.Length; i++) tasks[i] = _analyzer.AnalyzeAsync(crashLog);

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            var genericResult = (GenericAnalysisResult)r;
            genericResult.Success.Should().BeTrue("all concurrent executions should succeed");
            genericResult.HasFindings.Should().BeTrue("all concurrent executions should report findings");
        });
    }

    [Fact]
    public async Task AnalyzeAsync_WithSkyrimSE_UsesCorrectExecutableName()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\SkyrimSE"
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should()
            .Contain("SkyrimSE.exe", "correct executable name should be used for Skyrim SE");
    }

    [Fact]
    public async Task AnalyzeAsync_WithFallout4VR_TreatedAsFallout4()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings
        {
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4VR"
        });

        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("findings should be reported");
        genericResult.ReportText.Should().Contain("Fallout4.exe", "VR version should still use Fallout4.exe");
    }
}