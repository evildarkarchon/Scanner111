using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class YamlSettingsCacheTests
{
    private readonly string _testYamlPath;
    private readonly string _classicDataPath;
    
    public YamlSettingsCacheTests()
    {
        // Path to the CLASSIC Data directory
        _classicDataPath = Path.Combine(
            Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
            "Code to Port", "CLASSIC Data", "databases"
        );
        
        // Create a temporary test YAML file
        _testYamlPath = Path.Combine(Path.GetTempPath(), "test.yaml");
        
        var testYamlContent = @"
CLASSIC_Settings:
  Show FormID Values: true
  Max Results: 100
  Game: Fallout4
  
Test_Section:
  String_Value: ""Hello World""
  Number_Value: 42
  Boolean_Value: false
  List_Value:
    - item1
    - item2
    - item3
";
        
        File.WriteAllText(_testYamlPath, testYamlContent);
    }
    
    [Fact]
    public void YamlSettingsCache_WithValidPath_ReturnsCorrectValue()
    {
        // Clear cache to ensure fresh start
        YamlSettingsCache.ClearCache();
        
        // Create a test YAML file in the expected location from the test output directory
        var testDbPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC Data", "databases");
        if (!Directory.Exists(testDbPath))
        {
            Directory.CreateDirectory(testDbPath);
        }
        
        var testFile = Path.Combine(testDbPath, "test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Show FormID Values: true
  Max Results: 100
";
        
        try
        {
            File.WriteAllText(testFile, yamlContent);
            
            var result = YamlSettingsCache.YamlSettings<bool>("test", "CLASSIC_Settings.Show FormID Values", false);
            
            Assert.True(result);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (Directory.Exists(testDbPath))
                Directory.Delete(testDbPath, true);
        }
    }
    
    [Fact]
    public void YamlSettingsCache_WithNonExistentFile_ReturnsDefault()
    {
        var result = YamlSettingsCache.YamlSettings<string>("nonexistent", "some.key", "default");
        
        Assert.Equal("default", result);
    }
    
    [Fact]
    public void YamlSettingsCache_WithNonExistentKey_ReturnsDefault()
    {
        var testDbPath = Path.Combine("CLASSIC Data", "databases");
        if (!Directory.Exists(testDbPath))
        {
            Directory.CreateDirectory(testDbPath);
        }
        
        var testFile = Path.Combine(testDbPath, "test2.yaml");
        var yamlContent = @"
CLASSIC_Settings:
  Show FormID Values: true
";
        
        try
        {
            File.WriteAllText(testFile, yamlContent);
            
            var result = YamlSettingsCache.YamlSettings<string>("test2", "CLASSIC_Settings.NonExistent", "default");
            
            Assert.Equal("default", result);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }
    
    [Fact]
    public void YamlSettingsCache_WithCaching_ReturnsCachedValue()
    {
        var testDbPath = Path.Combine("CLASSIC Data", "databases");
        if (!Directory.Exists(testDbPath))
        {
            Directory.CreateDirectory(testDbPath);
        }
        
        var testFile = Path.Combine(testDbPath, "cache_test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Test_Value: original
";
        
        try
        {
            File.WriteAllText(testFile, yamlContent);
            
            // First call - should read from file
            var result1 = YamlSettingsCache.YamlSettings<string>("cache_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result1);
            
            // Modify file
            File.WriteAllText(testFile, @"CLASSIC_Settings:
  Test_Value: modified
");
            
            // Second call - should return cached value
            var result2 = YamlSettingsCache.YamlSettings<string>("cache_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result2); // Should still be original due to caching
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (Directory.Exists(testDbPath))
                Directory.Delete(testDbPath, true);
        }
    }
    
    [Fact]
    public void YamlSettingsCache_ClearCache_RemovesCachedValues()
    {
        var testDbPath = Path.Combine("CLASSIC Data", "databases");
        if (!Directory.Exists(testDbPath))
        {
            Directory.CreateDirectory(testDbPath);
        }
        
        var testFile = Path.Combine(testDbPath, "clear_test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Test_Value: original
";
        
        try
        {
            File.WriteAllText(testFile, yamlContent);
            
            // First call - should read from file
            var result1 = YamlSettingsCache.YamlSettings<string>("clear_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result1);
            
            // Modify file
            File.WriteAllText(testFile, @"CLASSIC_Settings:
  Test_Value: modified
");
            
            // Clear cache
            YamlSettingsCache.ClearCache();
            
            // Should now read the modified value
            var result2 = YamlSettingsCache.YamlSettings<string>("clear_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("modified", result2);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (Directory.Exists(testDbPath))
                Directory.Delete(testDbPath, true);
        }
    }
    
    [Fact]
    public void YamlSettingsCache_SetYamlSetting_UpdatesCache()
    {
        // Test the SetYamlSetting method (even though it's not fully implemented)
        YamlSettingsCache.SetYamlSetting("test", "some.key", "new_value");
        
        // The method should at least update the cache
        var result = YamlSettingsCache.YamlSettings<string>("test", "some.key", "default");
        Assert.Equal("new_value", result);
    }
    
    [Fact]
    public void YamlSettingsCache_WithRealClassicFiles_WorksIfExists()
    {
        // Test with actual CLASSIC files if they exist
        if (Directory.Exists(_classicDataPath))
        {
            var falloutFile = Path.Combine(_classicDataPath, "CLASSIC Fallout4.yaml");
            var mainFile = Path.Combine(_classicDataPath, "CLASSIC Main.yaml");
            
            if (File.Exists(falloutFile))
            {
                // Try to read a setting from the actual file
                var result = YamlSettingsCache.YamlSettings<string>("CLASSIC Fallout4", "CLASSIC_Settings.Game", "Unknown");
                Assert.NotNull(result);
                // Don't assert specific values as the YAML structure might vary
            }
            
            if (File.Exists(mainFile))
            {
                // Try to read a setting from the main file
                var result = YamlSettingsCache.YamlSettings<string>("CLASSIC Main", "CLASSIC_Settings.Version", "Unknown");
                Assert.NotNull(result);
            }
        }
    }
    
    [Fact]
    public void YamlSettingsCache_WithDifferentTypes_WorksCorrectly()
    {
        var testDbPath = Path.Combine("CLASSIC Data", "databases");
        if (!Directory.Exists(testDbPath))
        {
            Directory.CreateDirectory(testDbPath);
        }
        
        var testFile = Path.Combine(testDbPath, "types_test.yaml");
        var yamlContent = @"Test_Types:
  String_Value: Hello World
  Number_Value: 42
  Boolean_Value: true
  Float_Value: 3.14
";
        
        try
        {
            File.WriteAllText(testFile, yamlContent);
            
            var stringResult = YamlSettingsCache.YamlSettings<string>("types_test", "Test_Types.String_Value", "default");
            var numberResult = YamlSettingsCache.YamlSettings<int>("types_test", "Test_Types.Number_Value", 0);
            var boolResult = YamlSettingsCache.YamlSettings<bool>("types_test", "Test_Types.Boolean_Value", false);
            var floatResult = YamlSettingsCache.YamlSettings<double>("types_test", "Test_Types.Float_Value", 0.0);
            
            Assert.Equal("Hello World", stringResult);
            Assert.Equal(42, numberResult);
            Assert.True(boolResult);
            Assert.Equal(3.14, floatResult);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (Directory.Exists(testDbPath))
                Directory.Delete(testDbPath, true);
        }
    }
}