using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class GlobalRegistryTests
{
    public GlobalRegistryTests()
    {
        // Clear registry before each test
        GlobalRegistry.Clear();
    }
    
    [Fact]
    public void GlobalRegistry_SetAndGet_WorksCorrectly()
    {
        var testValue = "test value";
        
        GlobalRegistry.Set("testKey", testValue);
        var retrieved = GlobalRegistry.Get<string>("testKey");
        
        Assert.Equal(testValue, retrieved);
    }
    
    [Fact]
    public void GlobalRegistry_GetNonExistent_ReturnsNull()
    {
        var retrieved = GlobalRegistry.Get<string>("nonExistent");
        
        Assert.Null(retrieved);
    }
    
    [Fact]
    public void GlobalRegistry_GetValueType_WorksCorrectly()
    {
        GlobalRegistry.Set("intKey", 42);
        
        var retrieved = GlobalRegistry.GetValueType("intKey", 0);
        
        Assert.Equal(42, retrieved);
    }
    
    [Fact]
    public void GlobalRegistry_GetValueTypeNonExistent_ReturnsDefault()
    {
        var retrieved = GlobalRegistry.GetValueType("nonExistent", 99);
        
        Assert.Equal(99, retrieved);
    }
    
    [Fact]
    public void GlobalRegistry_Contains_WorksCorrectly()
    {
        GlobalRegistry.Set("existingKey", "value");
        
        Assert.True(GlobalRegistry.Contains("existingKey"));
        Assert.False(GlobalRegistry.Contains("nonExistentKey"));
    }
    
    [Fact]
    public void GlobalRegistry_Remove_WorksCorrectly()
    {
        GlobalRegistry.Set("removeKey", "value");
        
        Assert.True(GlobalRegistry.Contains("removeKey"));
        Assert.True(GlobalRegistry.Remove("removeKey"));
        Assert.False(GlobalRegistry.Contains("removeKey"));
        Assert.False(GlobalRegistry.Remove("removeKey")); // Second removal should return false
    }
    
    [Fact]
    public void GlobalRegistry_Clear_RemovesAllValues()
    {
        GlobalRegistry.Set("key1", "value1");
        GlobalRegistry.Set("key2", "value2");
        
        GlobalRegistry.Clear();
        
        Assert.False(GlobalRegistry.Contains("key1"));
        Assert.False(GlobalRegistry.Contains("key2"));
    }
    
    [Fact]
    public void GlobalRegistry_GameProperty_WorksCorrectly()
    {
        // Default value
        Assert.Equal("Fallout4", GlobalRegistry.Game);
        
        // Set custom value
        GlobalRegistry.Game = "Skyrim";
        Assert.Equal("Skyrim", GlobalRegistry.Game);
        
        // Verify it's stored in registry
        Assert.Equal("Skyrim", GlobalRegistry.Get<string>("Game"));
    }
    
    [Fact]
    public void GlobalRegistry_GameVRProperty_WorksCorrectly()
    {
        // Default value
        Assert.Equal("", GlobalRegistry.GameVR);
        
        // Set custom value
        GlobalRegistry.GameVR = "SkyrimVR";
        Assert.Equal("SkyrimVR", GlobalRegistry.GameVR);
    }
    
    [Fact]
    public void GlobalRegistry_LocalDirProperty_WorksCorrectly()
    {
        // Default value should be current domain base directory
        Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, GlobalRegistry.LocalDir);
        
        // Set custom value
        GlobalRegistry.LocalDir = @"C:\Custom\Path";
        Assert.Equal(@"C:\Custom\Path", GlobalRegistry.LocalDir);
    }
    
    [Fact]
    public void GlobalRegistry_ConfigProperty_WorksCorrectly()
    {
        // Default value
        Assert.Null(GlobalRegistry.Config);
        
        // Set custom value
        var config = new ClassicScanLogsInfo
        {
            ClassicVersion = "7.35.0",
            CrashgenName = "Buffout 4"
        };
        
        GlobalRegistry.Config = config;
        Assert.Equal(config, GlobalRegistry.Config);
        Assert.Equal("7.35.0", GlobalRegistry.Config.ClassicVersion);
    }
    
    [Fact]
    public void GlobalRegistry_ThreadSafety_WorksCorrectly()
    {
        // Test concurrent access
        var tasks = new List<Task>();
        
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                GlobalRegistry.Set($"key{taskId}", $"value{taskId}");
                var retrieved = GlobalRegistry.Get<string>($"key{taskId}");
                Assert.Equal($"value{taskId}", retrieved);
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // Verify all values are stored
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"value{i}", GlobalRegistry.Get<string>($"key{i}"));
        }
    }
}