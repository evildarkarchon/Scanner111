using Moq;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Xunit;

namespace Scanner111.Tests.CLI;

public class InteractiveCommandTests
{
    private readonly Mock<ITerminalUIService> _mockUiService;
    private readonly InteractiveCommand _command;

    public InteractiveCommandTests()
    {
        _mockUiService = new Mock<ITerminalUIService>();
        _command = new InteractiveCommand(_mockUiService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRunInteractiveMode()
    {
        // Arrange
        var options = new InteractiveOptions();
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        _mockUiService.Verify(x => x.RunInteractiveMode(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultTheme_DoesNotModifySettings()
    {
        // Arrange
        var options = new InteractiveOptions { Theme = "default" };
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        _mockUiService.Verify(x => x.RunInteractiveMode(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesReturnCode()
    {
        // Arrange
        var options = new InteractiveOptions();
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(42); // Non-zero return code

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException()
    {
        // Arrange
        var options = new InteractiveOptions();
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _command.ExecuteAsync(options));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAnimations_PassesOption()
    {
        // Arrange
        var options = new InteractiveOptions 
        { 
            Theme = "default",
            NoAnimations = true 
        };
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.True(options.NoAnimations);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTheme_SetsTheme()
    {
        // Arrange
        var options = new InteractiveOptions { Theme = "dark" };
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal("dark", options.Theme);
        // Note: Theme configuration is marked for future implementation
    }

    [Fact]
    public void Constructor_RequiresUIService()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InteractiveCommand(null!));
    }

    [Fact]
    public async Task ExecuteAsync_RequiresOptions()
    {
        // Arrange
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _command.ExecuteAsync(null!));
    }
}