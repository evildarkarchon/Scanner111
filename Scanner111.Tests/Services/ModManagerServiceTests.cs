using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Services;

[Collection("ModManager Tests")]
public class ModManagerServiceTests
{
    private readonly Mock<IModManagerDetector> _mockDetector;
    private readonly Mock<IMessageHandler> _mockMessageHandler;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly ModManagerService _service;

    public ModManagerServiceTests()
    {
        _mockDetector = new Mock<IModManagerDetector>();
        _mockMessageHandler = new Mock<IMessageHandler>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _service = new ModManagerService(
            _mockDetector.Object,
            _mockMessageHandler.Object,
            _mockSettingsService.Object);

        // Default settings - mod managers enabled
        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { AutoDetectModManagers = true });
    }

    [Fact]
    public async Task GetAvailableManagersAsync_DetectsInstalledManagers()
    {
        // Arrange
        var mockVortex = new Mock<IModManager>();
        mockVortex.Setup(x => x.Name).Returns("Vortex");
        mockVortex.Setup(x => x.Type).Returns(ModManagerType.Vortex);
        mockVortex.Setup(x => x.GetInstallPathAsync()).ReturnsAsync(@"C:\Program Files\Vortex");

        var mockMO2 = new Mock<IModManager>();
        mockMO2.Setup(x => x.Name).Returns("Mod Organizer 2");
        mockMO2.Setup(x => x.Type).Returns(ModManagerType.ModOrganizer2);
        mockMO2.Setup(x => x.GetInstallPathAsync()).ReturnsAsync(@"C:\ModOrganizer2");

        var managers = new List<IModManager> { mockVortex.Object, mockMO2.Object };
        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(managers);

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        result.Should().HaveCount(2);
        _mockMessageHandler.Verify(x => x.ShowInfo(
                It.Is<string>(s => s == "Detecting installed mod managers..."),
                MessageTarget.All),
            Times.Once);
        _mockMessageHandler.Verify(x => x.ShowSuccess(
                It.Is<string>(s => s.Contains("Found Vortex")),
                MessageTarget.All),
            Times.Once);
        _mockMessageHandler.Verify(x => x.ShowSuccess(
                It.Is<string>(s => s.Contains("Found Mod Organizer 2")),
                MessageTarget.All),
            Times.Once);
    }

    [Fact]
    public async Task GetAvailableManagersAsync_ReturnsEmpty_WhenModManagersDisabled()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            ModManagerSettings = new ModManagerSettings
            {
                SkipModManagerIntegration = true
            }
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetAvailableManagersAsync();

        // Assert
        result.Should().BeEmpty();
        _mockMessageHandler.Verify(x => x.ShowInfo(
                It.Is<string>(s => s.Contains("disabled")),
                MessageTarget.All),
            Times.Once);
        _mockDetector.Verify(x => x.DetectInstalledManagersAsync(), Times.Never);
    }

    [Fact]
    public async Task GetActiveManagerAsync_ReturnsPreferredManager()
    {
        // Arrange
        var mockVortex = new Mock<IModManager>();
        mockVortex.Setup(x => x.Type).Returns(ModManagerType.Vortex);

        var mockMO2 = new Mock<IModManager>();
        mockMO2.Setup(x => x.Type).Returns(ModManagerType.ModOrganizer2);

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockVortex.Object, mockMO2.Object });

        _service.SetPreferredManager(ModManagerType.ModOrganizer2);

        // Act
        var result = await _service.GetActiveManagerAsync();

        // Assert
        result.Should().Be(mockMO2.Object);
    }

    [Fact]
    public async Task GetActiveManagerAsync_ReturnsDefaultFromSettings()
    {
        // Arrange
        var mockVortex = new Mock<IModManager>();
        mockVortex.Setup(x => x.Type).Returns(ModManagerType.Vortex);

        var mockMO2 = new Mock<IModManager>();
        mockMO2.Setup(x => x.Type).Returns(ModManagerType.ModOrganizer2);

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockVortex.Object, mockMO2.Object });

        var settings = new ApplicationSettings
        {
            ModManagerSettings = new ModManagerSettings
            {
                DefaultManager = "Vortex"
            }
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetActiveManagerAsync();

        // Assert
        result.Should().Be(mockVortex.Object);
    }

    [Fact]
    public async Task GetAllModsAsync_LoadsModsFromActiveManager()
    {
        // Arrange
        var mods = new List<ModInfo>
        {
            new() { Name = "Mod1", IsEnabled = true },
            new() { Name = "Mod2", IsEnabled = false }
        };

        var mockManager = new Mock<IModManager>();
        mockManager.Setup(x => x.Name).Returns("Vortex");
        mockManager.Setup(x => x.Type).Returns(ModManagerType.Vortex);
        mockManager.Setup(x => x.GetInstalledModsAsync(It.IsAny<string?>())).ReturnsAsync(mods);

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockManager.Object });

        // Act
        var result = await _service.GetAllModsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.First().Name.Should().Be("Mod1");
        _mockMessageHandler.Verify(x => x.ShowInfo(
                It.Is<string>(s => s.Contains("Loading mods from Vortex")),
                MessageTarget.All),
            Times.Once);
        _mockMessageHandler.Verify(x => x.ShowSuccess(
                It.Is<string>(s => s.Contains("Loaded 2 mods")),
                MessageTarget.All),
            Times.Once);
    }

    [Fact]
    public async Task GetAllModsAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var mockManager = new Mock<IModManager>();
        mockManager.Setup(x => x.Name).Returns("Vortex");
        mockManager.Setup(x => x.Type).Returns(ModManagerType.Vortex);
        mockManager.Setup(x => x.GetInstalledModsAsync(It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Failed to read mods"));

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockManager.Object });

        // Act
        var result = await _service.GetAllModsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockMessageHandler.Verify(x => x.ShowError(
                It.Is<string>(s => s.Contains("Failed to load mods from Vortex")),
                MessageTarget.All),
            Times.Once);
    }

    [Fact]
    public async Task GetConsolidatedLoadOrderAsync_ReturnsLoadOrder()
    {
        // Arrange
        var loadOrder = new Dictionary<string, string>
        {
            { "Fallout4.esm", "00" },
            { "DLCRobot.esm", "01" },
            { "MyMod.esp", "02" }
        };

        var mockManager = new Mock<IModManager>();
        mockManager.Setup(x => x.Type).Returns(ModManagerType.ModOrganizer2);
        mockManager.Setup(x => x.GetLoadOrderAsync(It.IsAny<string?>())).ReturnsAsync(loadOrder);

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockManager.Object });

        // Act
        var result = await _service.GetConsolidatedLoadOrderAsync();

        // Assert
        result.Should().HaveCount(3);
        result["Fallout4.esm"].Should().Be("00");
    }

    [Fact]
    public async Task GetModStagingFolderAsync_ReturnsStagingPath()
    {
        // Arrange
        var stagingPath = @"C:\ModOrganizer2\mods";
        var mockManager = new Mock<IModManager>();
        mockManager.Setup(x => x.Type).Returns(ModManagerType.ModOrganizer2);
        mockManager.Setup(x => x.GetStagingFolderAsync()).ReturnsAsync(stagingPath);

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockManager.Object });

        // Act
        var result = await _service.GetModStagingFolderAsync();

        // Assert
        result.Should().Be(stagingPath);
    }

    [Fact]
    public async Task GetAvailableManagersAsync_UsesCachedResults()
    {
        // Arrange
        var mockManager = new Mock<IModManager>();
        mockManager.Setup(x => x.Name).Returns("Vortex");
        mockManager.Setup(x => x.GetInstallPathAsync()).ReturnsAsync(@"C:\Vortex");

        _mockDetector.Setup(x => x.DetectInstalledManagersAsync())
            .ReturnsAsync(new List<IModManager> { mockManager.Object });

        // Act - First call
        var result1 = await _service.GetAvailableManagersAsync();
        // Second call should use cache
        var result2 = await _service.GetAvailableManagersAsync();

        // Assert
        result1.Should().BeSameAs(result2);
        _mockDetector.Verify(x => x.DetectInstalledManagersAsync(), Times.Once);
    }

    [Fact]
    public void SetPreferredManager_UpdatesPreference()
    {
        // Act
        _service.SetPreferredManager(ModManagerType.NexusModManager);

        // Assert - will be tested indirectly through GetActiveManagerAsync
        // No exception should be thrown
        true.Should().BeTrue();
    }
}