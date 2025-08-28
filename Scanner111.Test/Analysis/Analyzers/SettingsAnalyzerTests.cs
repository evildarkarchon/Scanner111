using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Test.Analysis.Analyzers;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Analyzer")]
public class SettingsAnalyzerTests
{
    private readonly AnalysisContext _context;
    private readonly ILogger<SettingsAnalyzer> _logger;
    private readonly MemoryManagementValidator _memoryValidator;
    private readonly ISettingsService _settingsService;
    private readonly SettingsAnalyzer _sut;

    public SettingsAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<SettingsAnalyzer>>();
        _settingsService = Substitute.For<ISettingsService>();

        var memoryLogger = Substitute.For<ILogger<MemoryManagementValidator>>();
        _memoryValidator = new MemoryManagementValidator(memoryLogger);

        _sut = new SettingsAnalyzer(_logger, _settingsService, _memoryValidator);

        var yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _context = new AnalysisContext(@"C:\test\crashlog.txt", yamlCore);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnSuccess_WhenAllSettingsAreCorrect()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = new Version(1, 30, 0),
            Achievements = false,
            MemoryManager = true,
            ArchiveLimit = false,
            F4EE = true
        };

        var modSettings = new ModDetectionSettings
        {
            XseModules = new HashSet<string> { "f4ee.dll" },
            HasXCell = false,
            HasBakaScrapHeap = false
        };

        _settingsService.LoadCrashGenSettingsAsync(_context, Arg.Any<CancellationToken>())
            .Returns(crashGenSettings);
        _settingsService.LoadModDetectionSettingsAsync(_context, Arg.Any<CancellationToken>())
            .Returns(modSettings);

        // Act
        var result = await _sut.AnalyzeAsync(_context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Severity.Should().Be(AnalysisSeverity.Info);
    }

    [Fact]
    public async Task ScanAchievementsSettingAsync_ShouldReturnWarning_WhenAchievementsConflictExists()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Achievements = true // Conflict!
        };

        var modSettings = ModDetectionSettings.CreateWithXseModules(
            new[] { "achievements.dll" }); // Conflict!

        // Act
        var fragment = await _sut.ScanAchievementsSettingAsync(crashGenSettings, modSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("CAUTION");
        fragment.Content.Should().Contain("Achievements");
    }

    [Fact]
    public async Task ScanAchievementsSettingAsync_ShouldReturnSuccess_WhenNoConflict()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Achievements = false
        };

        var modSettings = ModDetectionSettings.CreateWithXseModules(
            new[] { "achievements.dll" });

        // Act
        var fragment = await _sut.ScanAchievementsSettingAsync(crashGenSettings, modSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("✔️");
        fragment.Content.Should().Contain("correctly configured");
    }

    [Fact]
    public async Task ScanMemoryManagementSettingsAsync_ShouldDetectXCellConflict()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            MemoryManager = true, // Conflict with X-Cell
            HavokMemorySystem = true // Also conflict
        };

        var modSettings = new ModDetectionSettings
        {
            HasXCell = true, // X-Cell installed
            XseModules = new HashSet<string> { "x-cell-ng2.dll" }
        };

        // Act
        var fragment = await _sut.ScanMemoryManagementSettingsAsync(crashGenSettings, modSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Type.Should().Be(FragmentType.Warning);
        fragment.Content.Should().Contain("X-Cell");
        fragment.Content.Should().Contain("MemoryManager");
    }

    [Fact]
    public async Task ScanArchiveLimitSettingAsync_ShouldSkipCheck_ForVersion129AndAbove()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = new Version(1, 29, 0),
            ArchiveLimit = true // Should be ignored
        };

        // Act
        var fragment = await _sut.ScanArchiveLimitSettingAsync(crashGenSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("skipped");
        fragment.Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public async Task ScanArchiveLimitSettingAsync_ShouldWarn_WhenArchiveLimitEnabled()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = new Version(1, 28, 0),
            ArchiveLimit = true // Problem!
        };

        // Act
        var fragment = await _sut.ScanArchiveLimitSettingAsync(crashGenSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("CAUTION");
        fragment.Content.Should().Contain("instability");
    }

    [Fact]
    public async Task ScanLooksMenuSettingAsync_ShouldDetectF4EEConflict()
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            F4EE = false // Conflict!
        };

        var modSettings = ModDetectionSettings.CreateWithXseModules(
            new[] { "f4ee.dll" }); // F4EE installed

        // Act
        var fragment = await _sut.ScanLooksMenuSettingAsync(crashGenSettings, modSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("CAUTION");
        fragment.Content.Should().Contain("Looks Menu");
        fragment.Content.Should().Contain("F4EE");
    }

    [Fact]
    public async Task CheckDisabledSettingsAsync_ShouldFindDisabledSettings()
    {
        // Arrange
        var rawSettings = new Dictionary<string, object>
        {
            ["Setting1"] = false,
            ["Setting2"] = true,
            ["Setting3"] = 0,
            ["IgnoredSetting"] = false
        };

        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            RawSettings = rawSettings,
            IgnoredSettings = new HashSet<string> { "IgnoredSetting" }
        };

        // Act
        var fragment = await _sut.CheckDisabledSettingsAsync(crashGenSettings);

        // Assert
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("Setting1");
        fragment.Content.Should().Contain("Setting3");
        fragment.Content.Should().NotContain("Setting2");
        fragment.Content.Should().NotContain("IgnoredSetting");
    }

    [Fact]
    public async Task PerformAnalysisAsync_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        _settingsService.LoadCrashGenSettingsAsync(_context, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CrashGenSettings>(new InvalidOperationException("Test exception")));

        // Act
        var result = await _sut.AnalyzeAsync(_context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Test exception"));
    }

    [Theory]
    [InlineData(true, false, false)] // MemManager enabled, no X-Cell, no Baka = OK
    [InlineData(false, true, false)] // MemManager disabled, X-Cell, no Baka = OK
    [InlineData(true, true, false)] // MemManager enabled, X-Cell = Conflict
    [InlineData(true, false, true)] // MemManager enabled, Baka = Conflict
    public async Task MemoryManagementValidator_ShouldValidateCorrectly(
        bool memManagerEnabled,
        bool hasXCell,
        bool hasBakaScrapHeap)
    {
        // Arrange
        var crashGenSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout",
            MemoryManager = memManagerEnabled
        };

        var modSettings = new ModDetectionSettings
        {
            HasXCell = hasXCell,
            HasBakaScrapHeap = hasBakaScrapHeap
        };

        // Act
        var fragment = await _sut.ScanMemoryManagementSettingsAsync(crashGenSettings, modSettings);

        // Assert
        fragment.Should().NotBeNull();

        var hasConflict = (memManagerEnabled && hasXCell) ||
                          (memManagerEnabled && hasBakaScrapHeap) ||
                          (hasXCell && hasBakaScrapHeap);

        if (hasConflict)
        {
            fragment.Type.Should().Be(FragmentType.Warning);
            fragment.Content.Should().Contain("CAUTION");
        }
        else if (memManagerEnabled || hasXCell)
        {
            fragment.Content.Should().Contain("✔️");
        }
    }
}