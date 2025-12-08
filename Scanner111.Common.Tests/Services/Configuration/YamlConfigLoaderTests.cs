using FluentAssertions;
using Scanner111.Common.Services.Configuration;

namespace Scanner111.Common.Tests.Services.Configuration;

public class YamlConfigLoaderTests : IDisposable
{
    private readonly YamlConfigLoader _loader;
    private readonly string _tempFile;

    public YamlConfigLoaderTests()
    {
        _loader = new YamlConfigLoader();
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_WithValidYaml_DeserializesCorrectly()
    {
        // Arrange
        var yaml = @"
            name: Test Item
            value: 123
        ";
        await File.WriteAllTextAsync(_tempFile, yaml);

        // Act
        var result = await _loader.LoadAsync<TestConfig>(_tempFile);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Item");
        result.Value.Should().Be(123);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingFile = "nonexistent.yaml";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _loader.LoadAsync<TestConfig>(missingFile));
    }

    [Fact]
    public async Task LoadDynamicAsync_WithValidYaml_ReturnsDictionary()
    {
        // Arrange
        var yaml = @"
            key1: value1
            key2: value2
        ";
        await File.WriteAllTextAsync(_tempFile, yaml);

        // Act
        var result = await _loader.LoadDynamicAsync(_tempFile);

        // Assert
        result.Should().ContainKey("key1");
        result["key1"].Should().Be("value1");
        result.Should().ContainKey("key2");
        result["key2"].Should().Be("value2");
    }

    private class TestConfig
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
