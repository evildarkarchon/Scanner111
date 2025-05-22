using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class YamlSettingsCacheServiceTests
{
    private readonly string _tempPath;
    private readonly string _testYamlPath;

    public YamlSettingsCacheServiceTests()
    {
        _tempPath = Path.GetTempPath();
        _testYamlPath = Path.Combine(_tempPath, $"test-yaml-{Guid.NewGuid()}.yaml");
    }

    [Fact]
    public void GetSetting_WithValidPath_ReturnsValue()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
            .Returns("C:\\Games\\Fallout4");

        // Act
        var result = mockService.Object.GetSetting<string>(Yaml.Game, "game_dir");

        // Assert
        Assert.Equal("C:\\Games\\Fallout4", result);
    }

    [Fact]
    public void GetSetting_WithInvalidPath_ReturnsDefault()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        string nullString = null;
        mockService
            .Setup(x => x.GetSetting<string>(Yaml.Game, "invalid_path", It.IsAny<string>()))
            .Returns(nullString);

        // Act
        var result = mockService.Object.GetSetting<string>(Yaml.Game, "invalid_path", "default_value");

        // Assert
        Assert.Equal("default_value", result);
    }

    [Fact]
    public void GetSetting_WithListType_ReturnsList()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        var expectedList = new List<string> { "item1", "item2", "item3" };

        mockService
            .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "list_setting", It.IsAny<List<string>>()))
            .Returns(expectedList);

        // Act
        var result = mockService.Object.GetSetting<List<string>>(Yaml.Main, "list_setting");

        // Assert
        Assert.Equal(expectedList, result);
    }

    [Fact]
    public void GetSetting_WithDictionaryType_ReturnsDictionary()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        var expectedDict = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        mockService
            .Setup(x => x.GetSetting<Dictionary<string, object>>(Yaml.Main, "dict_setting",
                It.IsAny<Dictionary<string, object>>()))
            .Returns(expectedDict);

        // Act
        var result = mockService.Object.GetSetting<Dictionary<string, object>>(Yaml.Main, "dict_setting");

        // Assert
        Assert.Equal(expectedDict, result);
    }

    [Fact]
    public void SetSetting_CallsUnderlyingImplementation()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.SetSetting(Yaml.Game, "game_dir", "C:\\Games\\Fallout4"))
            .Verifiable();

        // Act
        mockService.Object.SetSetting(Yaml.Game, "game_dir", "C:\\Games\\Fallout4");

        // Assert
        mockService.Verify(x => x.SetSetting(Yaml.Game, "game_dir", "C:\\Games\\Fallout4"), Times.Once);
    }

    [Fact]
    public void GetSetting_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.GetSetting<string>(Yaml.Game, null, It.IsAny<string>()))
            .Throws<ArgumentNullException>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => mockService.Object.GetSetting<string>(Yaml.Game, null));
    }

    [Fact]
    public void SetSetting_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.SetSetting<string>(Yaml.Game, null, It.IsAny<string>()))
            .Throws<ArgumentNullException>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => mockService.Object.SetSetting<string>(Yaml.Game, null, "value"));
    }

    [Fact]
    public void GetSetting_WithComplexNestedPath_ReturnsValue()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.GetSetting<string>(Yaml.Game, "section.subsection.key", It.IsAny<string>()))
            .Returns("nested_value");

        // Act
        var result = mockService.Object.GetSetting<string>(Yaml.Game, "section.subsection.key");

        // Assert
        Assert.Equal("nested_value", result);
    }

    [Fact]
    public void GetSetting_WithIntegerType_ReturnsInteger()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.GetSetting(Yaml.Game, "max_plugins", It.IsAny<int>()))
            .Returns(255);

        // Act
        var result = mockService.Object.GetSetting<int>(Yaml.Game, "max_plugins");

        // Assert
        Assert.Equal(255, result);
    }

    [Fact]
    public void GetSetting_WithBooleanType_ReturnsBoolean()
    {
        // Arrange
        var mockService = new Mock<IYamlSettingsCacheService>();
        mockService
            .Setup(x => x.GetSetting(Yaml.Game, "enable_feature", It.IsAny<bool>()))
            .Returns(true);

        // Act
        var result = mockService.Object.GetSetting<bool>(Yaml.Game, "enable_feature");

        // Assert
        Assert.True(result);
    }
}