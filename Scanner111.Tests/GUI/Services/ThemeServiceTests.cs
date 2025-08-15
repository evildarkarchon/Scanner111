using Scanner111.GUI.Models;
using Scanner111.GUI.Services;

namespace Scanner111.Tests.GUI.Services;

[Collection("GUI Tests")]
public class ThemeServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly ThemeService _service;
    private readonly UserSettings _testSettings;

    public ThemeServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _testSettings = new UserSettings
        {
            PreferredTheme = "Dark"
        };

        _mockSettingsService.Setup(x => x.LoadUserSettingsAsync())
            .ReturnsAsync(_testSettings);

        _mockSettingsService.Setup(x => x.SaveUserSettingsAsync(It.IsAny<UserSettings>()))
            .Returns(Task.CompletedTask);

        _service = new ThemeService(_mockSettingsService.Object);
    }

    [Fact]
    public void Constructor_LoadsSavedTheme()
    {
        _service.GetCurrentTheme().Should().Be("Dark");
        _mockSettingsService.Verify(x => x.LoadUserSettingsAsync(), Times.Once);
    }

    [Fact]
    public void GetCurrentTheme_ReturnsCurrentTheme()
    {
        _service.SetTheme("Light");

        _service.GetCurrentTheme().Should().Be("Light");
    }

    [Fact]
    public void SetTheme_ChangesCurrentTheme()
    {
        _service.SetTheme("Light");

        _service.GetCurrentTheme().Should().Be("Light");
        _mockSettingsService.Verify(x => x.SaveUserSettingsAsync(
                It.Is<UserSettings>(s => s.PreferredTheme == "Light")),
            Times.AtLeastOnce);
    }

    [Fact]
    public void SetTheme_IgnoresEmptyThemeName()
    {
        var initialTheme = _service.GetCurrentTheme();

        _service.SetTheme("");
        _service.SetTheme(null!);

        _service.GetCurrentTheme().Should().Be(initialTheme);
    }

    [Fact]
    public void SetTheme_IgnoresSameTheme()
    {
        _service.SetTheme("Dark"); // Already Dark

        // Should only be called once during constructor
        _mockSettingsService.Verify(x => x.LoadUserSettingsAsync(), Times.Once);
    }

    [Fact]
    public void SetTheme_HandlesLoadingFailure()
    {
        // Should not throw even if theme file doesn't exist
        _service.SetTheme("NonExistentTheme");

        // Theme should remain unchanged
        _service.GetCurrentTheme().Should().Be("Dark");
    }

    [Fact]
    public void Constructor_HandlesNullPreferredTheme()
    {
        _testSettings.PreferredTheme = null!;

        var service = new ThemeService(_mockSettingsService.Object);

        // Should default to Dark theme
        service.GetCurrentTheme().Should().Be("Dark");
    }

    [Fact]
    public void Constructor_HandlesSettingsLoadFailure()
    {
        _mockSettingsService.Setup(x => x.LoadUserSettingsAsync())
            .ThrowsAsync(new Exception("Load failed"));

        var service = new ThemeService(_mockSettingsService.Object);

        // Should default to Dark theme
        service.GetCurrentTheme().Should().Be("Dark");
    }

    [Fact]
    public void SetTheme_HandlesSaveFailure()
    {
        _mockSettingsService.Setup(x => x.SaveUserSettingsAsync(It.IsAny<UserSettings>()))
            .ThrowsAsync(new Exception("Save failed"));

        // Should not throw
        _service.SetTheme("Light");

        // Theme should still change even if save fails
        _service.GetCurrentTheme().Should().Be("Light");
    }
}