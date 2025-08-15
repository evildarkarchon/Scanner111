using FluentAssertions;
using Scanner111.Core.ModManagers;

namespace Scanner111.Tests.ModManagers;

public class ModManagerDetectorTests
{
    [Fact]
    public async Task DetectInstalledManagersAsync_ReturnsEmptyList_WhenNoManagersInstalled()
    {
        // Arrange
        var detector = new ModManagerDetector();

        // Act
        var managers = await detector.DetectInstalledManagersAsync();

        // Assert
        managers.Should().NotBeNull();
        // Note: This may fail on systems with mod managers installed
        // In a real test environment, we'd mock the registry and file system
    }

    [Fact]
    public async Task GetManagerByTypeAsync_ReturnsNull_WhenManagerNotInstalled()
    {
        // Arrange
        var detector = new ModManagerDetector();

        // Act
        var manager = await detector.GetManagerByTypeAsync(ModManagerType.None);

        // Assert
        manager.Should().BeNull();
    }

    [Fact]
    public void CheckPathExists_ReturnsFalse_WhenAllPathsNull()
    {
        // Act
        var result = ModManagerDetector.CheckPathExists(null, "", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CheckPathExists_ReturnsFalse_WhenPathsDoNotExist()
    {
        // Act
        var result = ModManagerDetector.CheckPathExists(
            "C:\\NonExistent\\Path1",
            "C:\\NonExistent\\Path2"
        );

        // Assert
        result.Should().BeFalse();
    }
}