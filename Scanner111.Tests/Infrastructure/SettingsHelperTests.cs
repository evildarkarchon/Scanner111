using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class SettingsHelperTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _originalAppData;

    public SettingsHelperTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "SettingsHelperTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
        
        // Store original APPDATA and set test directory
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA") ?? "";
        Environment.SetEnvironmentVariable("APPDATA", _testDirectory);
    }

    public void Dispose()
    {
        // Restore original APPDATA
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
        
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void GetSettingsDirectory_ReturnsCorrectPath()
    {
        // Act
        var directory = SettingsHelper.GetSettingsDirectory();
        
        // Assert
        directory.Should().EndWith(Path.Combine("Scanner111"));
    }

    [Fact]
    public void EnsureSettingsDirectoryExists_CreatesDirectory()
    {
        // Arrange
        var settingsDir = SettingsHelper.GetSettingsDirectory();
        
        // Act
        SettingsHelper.EnsureSettingsDirectoryExists();
        
        // Assert
        Directory.Exists(settingsDir).Should().BeTrue();
    }

    [Fact]
    public void EnsureSettingsDirectoryExists_MultipleCalls_DoesNotThrow()
    {
        // Act & Assert - Should not throw even if directory already exists
        SettingsHelper.EnsureSettingsDirectoryExists();
        SettingsHelper.EnsureSettingsDirectoryExists();
        SettingsHelper.EnsureSettingsDirectoryExists();
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNonExistentFile_CreatesDefaultAndSaves()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "settings.json");
        var defaultSettings = new TestSettings { Value = "Default", Number = 42 };
        
        // Act
        var loaded = await SettingsHelper.LoadSettingsAsync(filePath, () => defaultSettings);
        
        // Assert
        loaded.Should().NotBeNull();
        loaded.Value.Should().Be("Default");
        loaded.Number.Should().Be(42);
        File.Exists(filePath).Should().BeTrue();
        
        // Verify the file was saved with correct format
        var savedJson = await File.ReadAllTextAsync(filePath);
        var savedSettings = JsonSerializer.Deserialize<TestSettings>(savedJson, SettingsHelper.JsonOptions);
        savedSettings?.Value.Should().Be(defaultSettings.Value);
        savedSettings?.Number.Should().Be(defaultSettings.Number);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithExistingFile_LoadsCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "settings.json");
        var settings = new TestSettings { Value = "Saved", Number = 123 };
        
        // Create the settings directory
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        
        // Save initial settings
        var json = JsonSerializer.Serialize(settings, SettingsHelper.JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        
        // Act
        var loaded = await SettingsHelper.LoadSettingsAsync(filePath, () => new TestSettings());
        
        // Assert
        loaded.Should().NotBeNull();
        loaded.Value.Should().Be("Saved");
        loaded.Number.Should().Be(123);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCorruptedFile_ReturnsDefault()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "{ invalid json content ]");
        
        var defaultSettings = new TestSettings { Value = "Default", Number = 99 };
        
        // Act
        var loaded = await SettingsHelper.LoadSettingsAsync(filePath, () => defaultSettings);
        
        // Assert
        loaded.Should().NotBeNull();
        loaded.Value.Should().Be("Default");
        loaded.Number.Should().Be(99);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNullDeserialization_ReturnsDefault()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "null");
        
        var defaultSettings = new TestSettings { Value = "Default", Number = 77 };
        
        // Act
        var loaded = await SettingsHelper.LoadSettingsAsync(filePath, () => defaultSettings);
        
        // Assert
        loaded.Should().NotBeNull();
        loaded.Value.Should().Be("Default");
        loaded.Number.Should().Be(77);
    }

    [Fact]
    public async Task SaveSettingsAsync_SavesWithCorrectFormat()
    {
        // Arrange
        var filePath = Path.Combine(SettingsHelper.GetSettingsDirectory(), "settings.json");
        var settings = new TestSettings { Value = "Test", Number = 456 };
        
        // Act
        await SettingsHelper.SaveSettingsAsync(filePath, settings);
        
        // Assert
        File.Exists(filePath).Should().BeTrue();
        
        var savedJson = await File.ReadAllTextAsync(filePath);
        savedJson.Should().Contain("\"value\":"); // camelCase property naming
        savedJson.Should().Contain("\"number\":");
        savedJson.Should().Contain("\n"); // WriteIndented = true
        
        // Verify it can be loaded back
        var loaded = JsonSerializer.Deserialize<TestSettings>(savedJson, SettingsHelper.JsonOptions);
        loaded?.Value.Should().Be(settings.Value);
        loaded?.Number.Should().Be(settings.Number);
    }

    [Fact]
    public async Task SaveSettingsAsync_CreatesSettingsDirectoryIfNotExists()
    {
        // Arrange
        // Temporarily change APPDATA to a new location
        var newAppData = Path.Combine(_testDirectory, "NewAppData");
        Environment.SetEnvironmentVariable("APPDATA", newAppData);
        
        var settingsPath = Path.Combine(SettingsHelper.GetSettingsDirectory(), "settings.json");
        var settings = new TestSettings { Value = "Test", Number = 789 };
        
        // Act
        await SettingsHelper.SaveSettingsAsync(settingsPath, settings);
        
        // Assert
        File.Exists(settingsPath).Should().BeTrue();
        Directory.Exists(SettingsHelper.GetSettingsDirectory()).Should().BeTrue();
        
        // Restore original APPDATA
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithWriteError_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "Scanner111", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        
        // Create a read-only file
        await File.WriteAllTextAsync(filePath, "existing content");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);
        
        var settings = new TestSettings { Value = "Test", Number = 123 };
        
        try
        {
            // Act & Assert
            var act = () => SettingsHelper.SaveSettingsAsync(filePath, settings);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            // Cleanup - remove read-only attribute
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Theory]
    [InlineData("true", typeof(bool), true)]
    [InlineData("True", typeof(bool), true)]
    [InlineData("TRUE", typeof(bool), true)]
    [InlineData("yes", typeof(bool), true)]
    [InlineData("Yes", typeof(bool), true)]
    [InlineData("1", typeof(bool), true)]
    [InlineData("on", typeof(bool), true)]
    [InlineData("On", typeof(bool), true)]
    [InlineData("false", typeof(bool), false)]
    [InlineData("False", typeof(bool), false)]
    [InlineData("FALSE", typeof(bool), false)]
    [InlineData("no", typeof(bool), false)]
    [InlineData("No", typeof(bool), false)]
    [InlineData("0", typeof(bool), false)]
    [InlineData("off", typeof(bool), false)]
    [InlineData("Off", typeof(bool), false)]
    public void ConvertValue_StringToBool_ConvertsCorrectly(string input, Type targetType, object expected)
    {
        // Act
        var result = SettingsHelper.ConvertValue(input, targetType);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertValue_InvalidBoolString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => SettingsHelper.ConvertValue("maybe", typeof(bool));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid boolean value*");
    }

    [Theory]
    [InlineData("123", typeof(int), 123)]
    [InlineData("-456", typeof(int), -456)]
    [InlineData("0", typeof(int), 0)]
    public void ConvertValue_StringToInt_ConvertsCorrectly(string input, Type targetType, object expected)
    {
        // Act
        var result = SettingsHelper.ConvertValue(input, targetType);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertValue_InvalidIntString_ThrowsFormatException()
    {
        // Act & Assert
        var act = () => SettingsHelper.ConvertValue("not-a-number", typeof(int));
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ConvertValue_NullValue_ReturnsNull()
    {
        // Act
        var result = SettingsHelper.ConvertValue(null!, typeof(string));
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(123, typeof(string), "123")]
    [InlineData(123.45, typeof(string), "123.45")]
    [InlineData(true, typeof(string), "True")]
    public void ConvertValue_OtherConversions_UsesChangeType(object input, Type targetType, object expected)
    {
        // Act
        var result = SettingsHelper.ConvertValue(input, targetType);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hello-world", "HelloWorld")]
    [InlineData("hello_world", "HelloWorld")]
    [InlineData("hello world", "HelloWorld")]
    [InlineData("hello-world_test case", "HelloWorldTestCase")]
    [InlineData("HELLO-WORLD", "HelloWorld")]
    [InlineData("hELLo-WoRLd", "HelloWorld")]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("hello", "Hello")]
    [InlineData("a-b-c-d", "ABCD")]
    [InlineData("test__multiple___underscores", "TestMultipleUnderscores")]
    [InlineData("  spaced  -  words  ", "SpacedWords")]
    public void ToPascalCase_VariousInputs_ConvertsCorrectly(string? input, string? expected)
    {
        // Act
        var result = SettingsHelper.ToPascalCase(input);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void JsonOptions_HasCorrectConfiguration()
    {
        // Act
        var options = SettingsHelper.JsonOptions;
        
        // Assert
        options.Should().NotBeNull();
        options.WriteIndented.Should().BeTrue();
        options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    // Test helper class
    private class TestSettings
    {
        public string Value { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}