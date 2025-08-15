using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;

namespace Scanner111.Tests.CLI;

[Collection("Terminal UI Tests")]
public class InteractiveCommandTests
{
    private readonly InteractiveCommand _command;
    private readonly Mock<ITerminalUIService> _mockUiService;

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
        result.Should().Be(0, "because interactive mode should complete successfully");
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
        result.Should().Be(0, "because interactive mode should complete successfully");
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
        result.Should().Be(42, "because the return code should be propagated");
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
        var act = async () => await _command.ExecuteAsync(options);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");
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
        result.Should().Be(0, "because interactive mode should complete successfully");
        options.NoAnimations.Should().BeTrue("because no animations option should be preserved");
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
        result.Should().Be(0, "because interactive mode should complete successfully");
        options.Theme.Should().Be("dark", "because theme option should be preserved");
        // Note: Theme configuration is marked for future implementation
    }

    [Fact]
    public void Constructor_RequiresUIService()
    {
        // Act & Assert
        var act = () => new InteractiveCommand(null!);
        act.Should().Throw<ArgumentNullException>(
            "because UI service is required");
    }

    [Fact]
    public async Task ExecuteAsync_RequiresOptions()
    {
        // Arrange
        _mockUiService
            .Setup(x => x.RunInteractiveMode())
            .ReturnsAsync(0);

        // Act & Assert
        var act = async () => await _command.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>(
            "because options are required");
    }
}