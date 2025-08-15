using FluentAssertions;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Services;

namespace Scanner111.Tests.ModManagers;

[Collection("ModManager Tests")]
public class ModManagerDisableTests
{
    private readonly Mock<IModManagerDetector> _mockDetector;
    private readonly Mock<IMessageHandler> _mockMessageHandler;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly ModManagerService _service;

    public ModManagerDisableTests()
    {
        _mockDetector = new Mock<IModManagerDetector>();
        _mockMessageHandler = new Mock<IMessageHandler>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _service = new ModManagerService(
            _mockDetector.Object,
            _mockMessageHandler.Object,
            _mockSettingsService.Object
        );
    }

    [Fact]
    public async Task GetAvailableManagersAsync_ReturnsEmpty_WhenSkipModManagerIntegrationIsTrue()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = true
            }
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        result.Should().BeEmpty();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
        _mockMessageHandler.Verify(m => m.ShowInfo(
            It.Is<string>(s => s.Contains("Mod manager integration is disabled")),
            It.IsAny<MessageTarget>()
        ), Times.Once);
    }

    [Fact]
    public async Task GetAvailableManagersAsync_ReturnsEmpty_WhenAutoDetectModManagersIsFalse()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoDetectModManagers = false
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        result.Should().BeEmpty();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
        _mockMessageHandler.Verify(m => m.ShowInfo(
            It.Is<string>(s => s.Contains("Mod manager integration is disabled")),
            It.IsAny<MessageTarget>()
        ), Times.Once);
    }

    [Fact]
    public async Task GetActiveManagerAsync_ReturnsNull_WhenModManagersDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = true
            }
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetActiveManagerAsync();

        // Assert
        result.Should().BeNull();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
    }

    [Fact]
    public async Task GetAllModsAsync_ReturnsEmpty_WhenModManagersDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoDetectModManagers = false
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetAllModsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
    }

    [Fact]
    public async Task GetConsolidatedLoadOrderAsync_ReturnsEmpty_WhenModManagersDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = true
            }
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetConsolidatedLoadOrderAsync();

        // Assert
        result.Should().BeEmpty();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
    }

    [Fact]
    public async Task GetModStagingFolderAsync_ReturnsNull_WhenModManagersDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoDetectModManagers = false
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _service.GetModStagingFolderAsync();

        // Assert
        result.Should().BeNull();
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
    }

    [Fact]
    public async Task ModManagerService_PerformsNormally_WhenNotDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoDetectModManagers = true,
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = false
            }
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        var mockMO2 = new Mock<IModManager>();
        mockMO2.Setup(m => m.Name).Returns("Mod Organizer 2");
        mockMO2.Setup(m => m.Type).Returns(ModManagerType.ModOrganizer2);
        mockMO2.Setup(m => m.GetInstallPathAsync()).ReturnsAsync("C:\\MO2");

        var managers = new List<IModManager> { mockMO2.Object };
        _mockDetector.Setup(d => d.DetectInstalledManagersAsync()).ReturnsAsync(managers);

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Mod Organizer 2");
        _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true, false, false)] // AutoDetect=true, Skip=false, should NOT be disabled
    [InlineData(false, false, true)] // AutoDetect=false, Skip=false, should be disabled
    [InlineData(true, true, true)] // AutoDetect=true, Skip=true, should be disabled
    [InlineData(false, true, true)] // AutoDetect=false, Skip=true, should be disabled
    public async Task ModManagerIntegration_DisabledState_FollowsSettings(
        bool autoDetect, bool skipIntegration, bool expectDisabled)
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            AutoDetectModManagers = autoDetect,
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = skipIntegration
            }
        };
        _mockSettingsService.Setup(s => s.LoadSettingsAsync()).ReturnsAsync(settings);

        if (!expectDisabled)
        {
            // Setup managers if we expect them to be detected
            var mockManager = new Mock<IModManager>();
            mockManager.Setup(m => m.Name).Returns("Test Manager");
            mockManager.Setup(m => m.GetInstallPathAsync()).ReturnsAsync("C:\\Test");
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(new List<IModManager> { mockManager.Object });
        }

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        if (expectDisabled)
        {
            result.Should().BeEmpty();
            _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Never);
        }
        else
        {
            result.Should().NotBeEmpty();
            _mockDetector.Verify(d => d.DetectInstalledManagersAsync(), Times.Once);
        }
    }
}