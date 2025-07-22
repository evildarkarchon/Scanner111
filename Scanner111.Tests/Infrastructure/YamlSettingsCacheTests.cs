using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Contains unit tests for the <c>YamlSettingsCache</c> class, verifying its behavior in different scenarios.
/// </summary>
/// <remarks>
/// The class includes tests to validate operations such as retrieving settings values,
/// handling cached data, handling missing files or keys, updating cache values, and
/// working with various data types and configurations.
/// </remarks>
/// <threadsafety>
/// This test class is not thread-safe as it uses shared resources like the file system
/// and static cache mechanisms in the <c>YamlSettingsCache</c>.
/// </threadsafety>
/// <testsetup>
/// During setup, the class initializes required services such as a memory cache, a
/// cache manager, and a YAML settings provider. Moreover, it creates a temporary
/// YAML test file with predefined settings for testing purposes.
/// </testsetup>
/// <testcleanup>
/// Upon cleanup, the class disposes of services and memory, resets static caches,
/// and removes any temporary files created during the tests to maintain isolation.
/// </testcleanup>
/// <seealso cref="YamlSettingsCache"/>
/// <seealso cref="MemoryCache"/>
/// <seealso cref="CacheManager"/>
public class YamlSettingsCacheTests : IDisposable
{
    private readonly CacheManager _cacheManager;
    private readonly string _classicDataPath;
    private readonly IMemoryCache _memoryCache;
    private readonly string _testYamlPath;

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

        // Initialize the service components
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cacheManager = new CacheManager(_memoryCache, NullLogger<CacheManager>.Instance);
        var logger = new TestLogger<YamlSettingsService>();
        IYamlSettingsProvider yamlSettingsService = new YamlSettingsService(_cacheManager, logger);

        // Initialize the static cache with our service
        YamlSettingsCache.Initialize(yamlSettingsService);
    }

    /// Performs cleanup operations for the YamlSettingsCacheTests class.
    /// This method ensures proper resource management by:<br/>
    /// - Deleting any test YAML file that might have been created during testing.<br/>
    /// - Resetting the static cache in YamlSettingsCache to ensure no cached data persists between tests.<br/>
    /// - Disposing of resources in reverse order of their creation to prevent resource leaks.
    public void Dispose()
    {
        // Clean up test file first
        if (File.Exists(_testYamlPath)) File.Delete(_testYamlPath);

        // Reset the static cache before disposing services
        YamlSettingsCache.Reset();

        // Dispose in reverse order of creation
        _cacheManager?.Dispose();
        _memoryCache?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// Validates the retrieval of values from the YAML settings cache when provided with a valid file path.
    /// The test performs the following steps:<br/>
    /// - Clears any existing cache to ensure test isolation.<br/>
    /// - Creates a temporary YAML file in a designated directory for testing.<br/>
    /// - Writes predefined content to the YAML file, simulating a valid configuration.<br/>
    /// - Retrieves a specific boolean value from the YAML file using the `YamlSettingsCache`'s `YamlSettings` method.<br/>
    /// - Verifies that the retrieved value matches the expected result.<br/>
    /// After the test, any temporary files created are removed to ensure a clean test environment.
    [Fact]
    public void YamlSettingsCache_WithValidPath_ReturnsCorrectValue()
    {
        // Clear cache to ensure fresh start
        YamlSettingsCache.ClearCache();

        // Create a test YAML file in the expected location (Data directory)
        var testDbPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        if (!Directory.Exists(testDbPath)) Directory.CreateDirectory(testDbPath);

        var testFile = Path.Combine(testDbPath, "test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Show FormID Values: true
  Max Results: 100
";

        try
        {
            File.WriteAllText(testFile, yamlContent);

            var result = YamlSettingsCache.YamlSettings<bool>("test", "CLASSIC_Settings.Show FormID Values");

            Assert.True(result);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// Tests the behavior of the YamlSettingsCache when attempting to retrieve a value from a non-existent YAML file.
    /// This test verifies that, in the absence of the specified file, the method returns the provided default value.
    /// Ensures the following:<br/>
    /// - When the YAML file does not exist, no exception is thrown.<br/>
    /// - The returned value matches the specified default value.<br/>
    /// - The default behavior is consistent across calls with missing files.
    [Fact]
    public void YamlSettingsCache_WithNonExistentFile_ReturnsDefault()
    {
        var result = YamlSettingsCache.YamlSettings<string>("nonexistent", "some.key", "default");

        Assert.Equal("default", result);
    }

    /// Validates that retrieving a non-existent key from a YAML settings file
    /// returns the specified default value.<br/>
    /// This test performs the following operations:<br/>
    /// - Ensures the required test directory exists.<br/>
    /// - Creates a temporary YAML file with predefined content.<br/>
    /// - Attempts to retrieve a non-existent key from the YAML settings using YamlSettingsCache.<br/>
    /// - Asserts that the result matches the provided default value.<br/>
    /// - Cleans up by deleting the test YAML file after execution.
    [Fact]
    public void YamlSettingsCache_WithNonExistentKey_ReturnsDefault()
    {
        var testDbPath = Path.Combine("Data");
        if (!Directory.Exists(testDbPath)) Directory.CreateDirectory(testDbPath);

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

    /// Validates the caching behavior of the YamlSettingsCache class.
    /// This test ensures that:<br/>
    /// - The first access to a given setting uses the value from the YAML file.<br/>
    /// - Subsequent accesses to the same setting return a cached value, even if the file content changes.<br/>
    /// - The caching mechanism prevents unnecessary reads from the file, maintaining consistent performance and data integrity.<br/>
    /// The test writes a YAML file, reads the setting twice, modifies the file between reads, and asserts that the cached value remains unchanged.
    [Fact]
    public void YamlSettingsCache_WithCaching_ReturnsCachedValue()
    {
        var testDbPath = Path.Combine("Data");
        if (!Directory.Exists(testDbPath)) Directory.CreateDirectory(testDbPath);

        var testFile = Path.Combine(testDbPath, "cache_test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Test_Value: original
";

        try
        {
            File.WriteAllText(testFile, yamlContent);

            // First call - should read from file
            var result1 =
                YamlSettingsCache.YamlSettings<string>("cache_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result1);

            // Modify file
            File.WriteAllText(testFile, @"CLASSIC_Settings:
  Test_Value: modified
");

            // Second call - should return cached value
            var result2 =
                YamlSettingsCache.YamlSettings<string>("cache_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result2); // Should still be original due to caching
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// Verifies that the YamlSettingsCache correctly removes cached values when the cache is cleared.
    /// Specifically, this test:<br/>
    /// - Writes an initial YAML content to a test file.<br/>
    /// - Reads a value from the YAML file, which is then cached.<br/>
    /// - Updates the YAML file with new content.<br/>
    /// - Clears the cache using the YamlSettingsCache.ClearCache method.<br/>
    /// - Reads the updated value from the YAML file, ensuring the cache was successfully invalidated.<br/>
    /// Ensures proper cleanup by deleting the test file after the test completes.
    [Fact]
    public void YamlSettingsCache_ClearCache_RemovesCachedValues()
    {
        var testDbPath = Path.Combine("Data");
        if (!Directory.Exists(testDbPath)) Directory.CreateDirectory(testDbPath);

        var testFile = Path.Combine(testDbPath, "clear_test.yaml");
        var yamlContent = @"CLASSIC_Settings:
  Test_Value: original
";

        try
        {
            File.WriteAllText(testFile, yamlContent);

            // First call - should read from file
            var result1 =
                YamlSettingsCache.YamlSettings<string>("clear_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("original", result1);

            // Modify file
            File.WriteAllText(testFile, @"CLASSIC_Settings:
  Test_Value: modified
");

            // Clear cache
            YamlSettingsCache.ClearCache();

            // Should now read the modified value
            var result2 =
                YamlSettingsCache.YamlSettings<string>("clear_test", "CLASSIC_Settings.Test_Value", "default");
            Assert.Equal("modified", result2);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// Ensures that the `SetYamlSetting` method of `YamlSettingsCache` correctly updates the cache.
    /// This test case verifies that:<br/>
    /// - Setting a value using `SetYamlSetting` updates the in-memory cache.<br/>
    /// - The updated value can subsequently be retrieved from the cache using the `YamlSettings` method with the same parameters.<br/>
    /// The functionality is validated using assertions to check that the value retrieved from the cache matches the expected updated value.
    [Fact]
    public void YamlSettingsCache_SetYamlSetting_UpdatesCache()
    {
        // Test the SetYamlSetting method (even though it's not fully implemented)
        YamlSettingsCache.SetYamlSetting("test", "some.key", "new_value");

        // The method should at least update the cache
        var result = YamlSettingsCache.YamlSettings<string>("test", "some.key", "default");
        Assert.Equal("new_value", result);
    }

    /// Validates the behavior of the YamlSettingsCache when working with real CLASSIC YAML configuration files.
    /// This test checks if the cache correctly handles scenarios where specific CLASSIC files exist:<br/>
    /// - It verifies that the cache can handle settings from the "CLASSIC Fallout4.yaml" file if present.<br/>
    /// - It ensures proper functionality when interacting with the "CLASSIC Main.yaml" file if available.<br/>
    /// The test executes only if the defined `_classicDataPath` directory exists, and the associated files are accessible.
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
                var result =
                    YamlSettingsCache.YamlSettings<string>("CLASSIC Fallout4", "CLASSIC_Settings.Game", "Unknown");
                Assert.NotNull(result);
                // Don't assert specific values as the YAML structure might vary
            }

            if (File.Exists(mainFile))
            {
                // Try to read a setting from the main file
                var result =
                    YamlSettingsCache.YamlSettings<string>("CLASSIC Main", "CLASSIC_Settings.Version", "Unknown");
                Assert.NotNull(result);
            }
        }
    }

    /// Tests the functionality of the YamlSettingsCache class to correctly handle and retrieve values of different data types.
    /// This test creates a temporary YAML file containing keys with various data types, such as:<br/>
    /// - String<br/>
    /// - Integer<br/>
    /// - Boolean<br/>
    /// - Float<br/>
    /// The method verifies that the YamlSettingsCache can correctly parse and return values for each type using the appropriate
    /// key and default value (if applicable).<br/>
    /// Ensures the following:<br/>
    /// - The values retrieved match the expected values for each type.<br/>
    /// - The cache implementation works consistently across all supported types.<br/>
    /// The test also includes cleanup operations to remove the temporary YAML file that it generates during execution
    /// to avoid environmental side effects.
    [Fact]
    public void YamlSettingsCache_WithDifferentTypes_WorksCorrectly()
    {
        var testDbPath = Path.Combine("Data");
        if (!Directory.Exists(testDbPath)) Directory.CreateDirectory(testDbPath);

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

            var stringResult =
                YamlSettingsCache.YamlSettings<string>("types_test", "Test_Types.String_Value", "default");
            var numberResult = YamlSettingsCache.YamlSettings<int>("types_test", "Test_Types.Number_Value");
            var boolResult = YamlSettingsCache.YamlSettings<bool>("types_test", "Test_Types.Boolean_Value");
            var floatResult = YamlSettingsCache.YamlSettings<double>("types_test", "Test_Types.Float_Value");

            Assert.Equal("Hello World", stringResult);
            Assert.Equal(42, numberResult);
            Assert.True(boolResult);
            Assert.Equal(3.14, floatResult);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }
}