using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
        Assert.EndsWith(Path.Combine("Scanner111"), directory);
    }

    [Fact]
    public void EnsureSettingsDirectoryExists_CreatesDirectory()
    {
        // Arrange
        var settingsDir = SettingsHelper.GetSettingsDirectory();
        
        // Act
        SettingsHelper.EnsureSettingsDirectoryExists();
        
        // Assert
        Assert.True(Directory.Exists(settingsDir));
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
        Assert.NotNull(loaded);
        Assert.Equal("Default", loaded.Value);
        Assert.Equal(42, loaded.Number);
        Assert.True(File.Exists(filePath));
        
        // Verify the file was saved with correct format
        var savedJson = await File.ReadAllTextAsync(filePath);
        var savedSettings = JsonSerializer.Deserialize<TestSettings>(savedJson, SettingsHelper.JsonOptions);
        Assert.Equal(defaultSettings.Value, savedSettings?.Value);
        Assert.Equal(defaultSettings.Number, savedSettings?.Number);
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
        Assert.NotNull(loaded);
        Assert.Equal("Saved", loaded.Value);
        Assert.Equal(123, loaded.Number);
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
        Assert.NotNull(loaded);
        Assert.Equal("Default", loaded.Value);
        Assert.Equal(99, loaded.Number);
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
        Assert.NotNull(loaded);
        Assert.Equal("Default", loaded.Value);
        Assert.Equal(77, loaded.Number);
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
        Assert.True(File.Exists(filePath));
        
        var savedJson = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"value\":", savedJson); // camelCase property naming
        Assert.Contains("\"number\":", savedJson);
        Assert.Contains("\n", savedJson); // WriteIndented = true
        
        // Verify it can be loaded back
        var loaded = JsonSerializer.Deserialize<TestSettings>(savedJson, SettingsHelper.JsonOptions);
        Assert.Equal(settings.Value, loaded?.Value);
        Assert.Equal(settings.Number, loaded?.Number);
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
        Assert.True(File.Exists(settingsPath));
        Assert.True(Directory.Exists(SettingsHelper.GetSettingsDirectory()));
        
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
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => SettingsHelper.SaveSettingsAsync(filePath, settings));
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertValue_InvalidBoolString_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            SettingsHelper.ConvertValue("maybe", typeof(bool)));
        Assert.Contains("Invalid boolean value", ex.Message);
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertValue_InvalidIntString_ThrowsFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => 
            SettingsHelper.ConvertValue("not-a-number", typeof(int)));
    }

    [Fact]
    public void ConvertValue_NullValue_ReturnsNull()
    {
        // Act
        var result = SettingsHelper.ConvertValue(null!, typeof(string));
        
        // Assert
        Assert.Null(result);
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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void JsonOptions_HasCorrectConfiguration()
    {
        // Act
        var options = SettingsHelper.JsonOptions;
        
        // Assert
        Assert.NotNull(options);
        Assert.True(options.WriteIndented);
        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
    }

    // Test helper class
    private class TestSettings
    {
        public string Value { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}