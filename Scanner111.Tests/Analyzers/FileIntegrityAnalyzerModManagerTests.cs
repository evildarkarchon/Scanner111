using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Analyzers;

[Collection("ModManager Tests")]
public class FileIntegrityAnalyzerModManagerTests
{
    private readonly FileIntegrityAnalyzer _analyzer;
    private readonly Mock<IHashValidationService> _mockHashService;
    private readonly Mock<IMessageHandler> _mockMessageHandler;
    private readonly Mock<IModManagerService> _mockModManagerService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly Mock<IYamlSettingsProvider> _mockYamlSettings;
    private readonly Mock<IGamePathDetection> _mockGamePathDetection;

    public FileIntegrityAnalyzerModManagerTests()
    {
        _mockHashService = new Mock<IHashValidationService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockYamlSettings = new Mock<IYamlSettingsProvider>();
        _mockMessageHandler = new Mock<IMessageHandler>();
        _mockModManagerService = new Mock<IModManagerService>();
        _mockGamePathDetection = new Mock<IGamePathDetection>();

        _analyzer = new FileIntegrityAnalyzer(
            _mockHashService.Object,
            _mockSettingsService.Object,
            _mockYamlSettings.Object,
            _mockMessageHandler.Object,
            _mockGamePathDetection.Object,
            _mockModManagerService.Object
        );
    }

    [Fact]
    public async Task AnalyzeAsync_SkipsModManagerCheck_WhenAutoDetectModManagersIsFalse()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            FcxMode = true,
            AutoDetectModManagers = false,
            GamePath = "C:\\Games\\Fallout4"
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var crashLog = new CrashLog
        {
            GamePath = "C:\\Games\\Fallout4",
            FilePath = "crash-test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FcxScanResult>();

        // Verify that mod manager service was never called
        _mockModManagerService.Verify(m => m.GetActiveManagerAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetAllModsAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetModStagingFolderAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetConsolidatedLoadOrderAsync(), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_SkipsModManagerCheck_WhenSkipModManagerIntegrationIsTrue()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            FcxMode = true,
            AutoDetectModManagers = true,
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = true
            },
            GamePath = "C:\\Games\\Fallout4"
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var crashLog = new CrashLog
        {
            GamePath = "C:\\Games\\Fallout4",
            FilePath = "crash-test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FcxScanResult>();

        // Verify that mod manager service was never called
        _mockModManagerService.Verify(m => m.GetActiveManagerAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetAllModsAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetModStagingFolderAsync(), Times.Never);
        _mockModManagerService.Verify(m => m.GetConsolidatedLoadOrderAsync(), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_CallsModManagerService_WhenEnabledAndInFcxMode()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            FcxMode = true,
            AutoDetectModManagers = true,
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = false
            },
            GamePath = "C:\\Games\\Fallout4"
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var crashLog = new CrashLog
        {
            GamePath = "C:\\Games\\Fallout4",
            FilePath = "crash-test.log"
        };

        var mockModManager = new Mock<IModManager>();
        mockModManager.SetupGet(m => m.Name).Returns("Mod Organizer 2");

        _mockModManagerService.Setup(m => m.GetActiveManagerAsync())
            .ReturnsAsync(mockModManager.Object);
        _mockModManagerService.Setup(m => m.GetAllModsAsync())
            .ReturnsAsync(new List<ModInfo>());
        _mockModManagerService.Setup(m => m.GetModStagingFolderAsync())
            .ReturnsAsync("C:\\MO2\\mods");
        _mockModManagerService.Setup(m => m.GetConsolidatedLoadOrderAsync())
            .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FcxScanResult>();

        // Verify that mod manager service was called
        _mockModManagerService.Verify(m => m.GetActiveManagerAsync(), Times.Once);
        _mockModManagerService.Verify(m => m.GetModStagingFolderAsync(), Times.Once);
        _mockModManagerService.Verify(m => m.GetAllModsAsync(), Times.Once);
        _mockModManagerService.Verify(m => m.GetConsolidatedLoadOrderAsync(), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesNullModManagerService_Gracefully()
    {
        // Arrange
        var analyzerWithoutModManager = new FileIntegrityAnalyzer(
            _mockHashService.Object,
            _mockSettingsService.Object,
            _mockYamlSettings.Object,
            _mockMessageHandler.Object,
            _mockGamePathDetection.Object // No mod manager service - optional parameter
        );

        var settings = new ApplicationSettings
        {
            FcxMode = true,
            AutoDetectModManagers = true,
            GamePath = "C:\\Games\\Fallout4"
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var crashLog = new CrashLog
        {
            GamePath = "C:\\Games\\Fallout4",
            FilePath = "crash-test.log"
        };

        // Act
        var result = await analyzerWithoutModManager.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FcxScanResult>();
        result.Success.Should().BeTrue();

        // Should not throw any exceptions when mod manager service is null
    }

    [Theory]
    [InlineData(true, true, false, true)] // FCX on, AutoDetect on, Skip off = should check
    [InlineData(true, false, false, false)] // FCX on, AutoDetect off = should NOT check
    [InlineData(true, true, true, false)] // FCX on, AutoDetect on, Skip on = should NOT check
    [InlineData(false, true, false, false)] // FCX off = should NOT check regardless
    public async Task AnalyzeAsync_ModManagerCheck_DependsOnSettings(
        bool fcxMode, bool autoDetect, bool skipIntegration, bool expectModManagerCheck)
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            FcxMode = fcxMode,
            AutoDetectModManagers = autoDetect,
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = skipIntegration
            },
            GamePath = "C:\\Games\\Fallout4"
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var crashLog = new CrashLog
        {
            GamePath = "C:\\Games\\Fallout4",
            FilePath = "crash-test.log"
        };

        if (expectModManagerCheck)
        {
            var mockModManager = new Mock<IModManager>();
            mockModManager.SetupGet(m => m.Name).Returns("Test Manager");
            _mockModManagerService.Setup(m => m.GetActiveManagerAsync())
                .ReturnsAsync(mockModManager.Object);
            _mockModManagerService.Setup(m => m.GetAllModsAsync())
                .ReturnsAsync(new List<ModInfo>());
            _mockModManagerService.Setup(m => m.GetModStagingFolderAsync())
                .ReturnsAsync("C:\\Test\\mods");
            _mockModManagerService.Setup(m => m.GetConsolidatedLoadOrderAsync())
                .ReturnsAsync(new Dictionary<string, string>());
        }

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();

        if (expectModManagerCheck)
            _mockModManagerService.Verify(m => m.GetActiveManagerAsync(), Times.Once);
        else
            _mockModManagerService.Verify(m => m.GetActiveManagerAsync(), Times.Never);
    }
}