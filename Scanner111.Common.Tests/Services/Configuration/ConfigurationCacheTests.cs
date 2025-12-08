using FluentAssertions;
using Moq;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;

namespace Scanner111.Common.Tests.Services.Configuration;

public class ConfigurationCacheTests
{
    private readonly Mock<IYamlConfigLoader> _loader;
    private readonly ConfigurationCache _cache;

    public ConfigurationCacheTests()
    {
        _loader = new Mock<IYamlConfigLoader>();
        _cache = new ConfigurationCache(_loader.Object, "TestData");
    }

    [Fact]
    public async Task GetGameConfigAsync_FirstCall_LoadsFromLoader()
    {
        // Arrange
        var gameName = "TestGame";
        var expectedConfig = new GameConfiguration { GameName = gameName };
        
        _loader.Setup(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _cache.GetGameConfigAsync(gameName);

        // Assert
        result.Should().Be(expectedConfig);
        _loader.Verify(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGameConfigAsync_SecondCall_ReturnsCachedValue()
    {
        // Arrange
        var gameName = "TestGame";
        var expectedConfig = new GameConfiguration { GameName = gameName };
        
        _loader.Setup(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        await _cache.GetGameConfigAsync(gameName);
        var result = await _cache.GetGameConfigAsync(gameName);

        // Assert
        result.Should().Be(expectedConfig);
        // Loader should still only be called once
        _loader.Verify(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSuspectPatternsAsync_LoadsCorrectly()
    {
        // Arrange
        var gameName = "TestGame";
        var expectedPatterns = new SuspectPatterns();
        
        _loader.Setup(x => x.LoadAsync<SuspectPatterns>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPatterns);

        // Act
        var result = await _cache.GetSuspectPatternsAsync(gameName);

        // Assert
        result.Should().Be(expectedPatterns);
    }
    
    [Fact]
    public void Clear_ClearsCache()
    {
        // Arrange
        var gameName = "TestGame";
        _loader.Setup(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameConfiguration());

        // Act
        _cache.GetGameConfigAsync(gameName).Wait();
        _cache.Clear();
        _cache.GetGameConfigAsync(gameName).Wait();

        // Assert
        // Loader should be called twice (once before clear, once after)
        _loader.Verify(x => x.LoadAsync<GameConfiguration>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
