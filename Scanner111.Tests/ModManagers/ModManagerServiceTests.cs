using FluentAssertions;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Tests.ModManagers
{
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
                _mockSettingsService.Object
            );
        }

        [Fact]
        public async Task GetAvailableManagersAsync_ReturnsDetectedManagers()
        {
            // Arrange
            var mockMO2 = new Mock<IModManager>();
            mockMO2.Setup(m => m.Name).Returns("Mod Organizer 2");
            mockMO2.Setup(m => m.Type).Returns(ModManagerType.ModOrganizer2);
            mockMO2.Setup(m => m.GetInstallPathAsync()).ReturnsAsync("C:\\MO2");

            var managers = new List<IModManager> { mockMO2.Object };
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(managers);

            // Act
            var result = await _service.GetAvailableManagersAsync();

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Mod Organizer 2");
            
            _mockMessageHandler.Verify(m => m.ShowSuccess(
                It.Is<string>(s => s.Contains("Found Mod Organizer 2")),
                It.IsAny<MessageTarget>()
            ), Times.Once);
        }

        [Fact]
        public async Task GetAvailableManagersAsync_ShowsInfoMessage_WhenNoManagersFound()
        {
            // Arrange
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(new List<IModManager>());

            // Act
            var result = await _service.GetAvailableManagersAsync();

            // Assert
            result.Should().BeEmpty();
            
            _mockMessageHandler.Verify(m => m.ShowInfo(
                It.Is<string>(s => s.Contains("No mod managers detected")),
                It.IsAny<MessageTarget>()
            ), Times.Once);
        }

        [Fact]
        public async Task GetActiveManagerAsync_ReturnsPreferredManager_WhenSet()
        {
            // Arrange
            var mockMO2 = new Mock<IModManager>();
            mockMO2.Setup(m => m.Type).Returns(ModManagerType.ModOrganizer2);
            
            var mockVortex = new Mock<IModManager>();
            mockVortex.Setup(m => m.Type).Returns(ModManagerType.Vortex);

            var managers = new List<IModManager> { mockMO2.Object, mockVortex.Object };
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(managers);

            _service.SetPreferredManager(ModManagerType.Vortex);

            // Act
            var result = await _service.GetActiveManagerAsync();

            // Assert
            result.Should().Be(mockVortex.Object);
        }

        [Fact]
        public async Task GetActiveManagerAsync_ReturnsDefaultFromSettings_WhenNoPreferred()
        {
            // Arrange
            var mockMO2 = new Mock<IModManager>();
            mockMO2.Setup(m => m.Type).Returns(ModManagerType.ModOrganizer2);

            var managers = new List<IModManager> { mockMO2.Object };
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(managers);

            var settings = new ApplicationSettings
            {
                ModManagerSettings = new ModManagerSettings
                {
                    DefaultManager = "ModOrganizer2"
                }
            };
            _mockSettingsService.Setup(s => s.LoadSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            var result = await _service.GetActiveManagerAsync();

            // Assert
            result.Should().Be(mockMO2.Object);
        }

        [Fact]
        public async Task GetAllModsAsync_ReturnsEmpty_WhenNoManagerActive()
        {
            // Arrange
            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(new List<IModManager>());

            // Act
            var result = await _service.GetAllModsAsync();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllModsAsync_ReturnsModsFromActiveManager()
        {
            // Arrange
            var mods = new List<ModInfo>
            {
                new ModInfo { Name = "Mod1", IsEnabled = true },
                new ModInfo { Name = "Mod2", IsEnabled = false }
            };

            var mockManager = new Mock<IModManager>();
            mockManager.Setup(m => m.Name).Returns("Test Manager");
            mockManager.Setup(m => m.GetInstalledModsAsync(null))
                .ReturnsAsync(mods);

            _mockDetector.Setup(d => d.DetectInstalledManagersAsync())
                .ReturnsAsync(new List<IModManager> { mockManager.Object });

            var settings = new ApplicationSettings();
            _mockSettingsService.Setup(s => s.LoadSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            var result = await _service.GetAllModsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(m => m.Name == "Mod1");
            result.Should().Contain(m => m.Name == "Mod2");
        }

        [Fact]
        public void SetPreferredManager_SetsPreference()
        {
            // Act
            _service.SetPreferredManager(ModManagerType.ModOrganizer2);

            // Assert
            // This is tested indirectly through GetActiveManagerAsync test above
            // In a real implementation, we might expose a property to verify this
        }
    }
}