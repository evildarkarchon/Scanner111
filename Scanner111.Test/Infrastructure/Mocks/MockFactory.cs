using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Test.Infrastructure.Mocks;

/// <summary>
///     Factory for creating commonly used mock objects with default configurations.
/// </summary>
public static class MockFactory
{
    /// <summary>
    ///     Creates a mock IAsyncYamlSettingsCore with common default settings.
    /// </summary>
    public static IAsyncYamlSettingsCore CreateYamlCore(
        Action<IAsyncYamlSettingsCore>? configure = null)
    {
        var mock = Substitute.For<IAsyncYamlSettingsCore>();
        
        // Default empty list responses
        mock.GetSettingAsync<List<string>>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<List<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));
        
        // Default empty dictionary responses
        mock.GetSettingAsync<Dictionary<string, string>>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Dictionary<string, string>?>(new Dictionary<string, string>()));
        
        // Default null string responses
        mock.GetSettingAsync<string>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
        
        // Apply custom configuration if provided
        configure?.Invoke(mock);
        
        return mock;
    }

    /// <summary>
    ///     Creates a mock IPluginLoader with default behavior.
    /// </summary>
    public static IPluginLoader CreatePluginLoader(
        Dictionary<string, string>? plugins = null,
        bool hasLoadOrder = false,
        Action<IPluginLoader>? configure = null)
    {
        var mock = Substitute.For<IPluginLoader>();
        
        plugins ??= new Dictionary<string, string>();
        
        // Default load order response
        var loadOrderFragment = ReportFragment.CreateInfo(
            "Load Order Status",
            hasLoadOrder ? "Loadorder.txt found" : "No loadorder.txt found");
            
        mock.LoadFromLoadOrderFileAsync(
                Arg.Any<string?>(), 
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((plugins, hasLoadOrder, loadOrderFragment)));
        
        // Default scan response
        mock.ScanPluginsFromLog(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Version>(),
                Arg.Any<Version>(),
                Arg.Any<ISet<string>?>())
            .Returns((plugins, false, false));
        
        // Default statistics
        mock.GetStatistics()
            .Returns(new PluginLoadingStatistics
            {
                LoadOrderPluginCount = hasLoadOrder ? plugins.Count : 0,
                CrashLogPluginCount = hasLoadOrder ? 0 : plugins.Count,
                IgnoredPluginCount = 0
            });
        
        // Default validation
        mock.ValidateLoadOrderFileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(hasLoadOrder));
        
        configure?.Invoke(mock);
        
        return mock;
    }

    /// <summary>
    ///     Creates a mock ISettingsService with default settings.
    /// </summary>
    public static ISettingsService CreateSettingsService(
        CrashGenSettings? crashGenSettings = null,
        ModDetectionSettings? modSettings = null,
        Action<ISettingsService>? configure = null)
    {
        var mock = Substitute.For<ISettingsService>();
        
        crashGenSettings ??= new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = new Version(1, 30, 0),
            Achievements = false,
            MemoryManager = true,
            ArchiveLimit = false,
            F4EE = true
        };
        
        modSettings ??= new ModDetectionSettings
        {
            XseModules = new HashSet<string> { "f4ee.dll" },
            HasXCell = false,
            HasBakaScrapHeap = false
        };
        
        mock.LoadCrashGenSettingsAsync(
                Arg.Any<AnalysisContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(crashGenSettings));
        
        mock.LoadModDetectionSettingsAsync(
                Arg.Any<AnalysisContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(modSettings));
        
        configure?.Invoke(mock);
        
        return mock;
    }

    /// <summary>
    ///     Creates a mock logger of the specified type.
    /// </summary>
    public static ILogger<T> CreateLogger<T>()
    {
        return Substitute.For<ILogger<T>>();
    }

    /// <summary>
    ///     Creates an AnalysisContext with the specified shared data.
    /// </summary>
    public static AnalysisContext CreateAnalysisContext(
        string logPath = @"C:\test\crashlog.txt",
        IAsyncYamlSettingsCore? yamlCore = null,
        params (string Key, object Value)[] sharedData)
    {
        yamlCore ??= CreateYamlCore();
        var context = new AnalysisContext(logPath, yamlCore);
        
        foreach (var (key, value) in sharedData)
        {
            context.SetSharedData(key, value);
        }
        
        return context;
    }

    /// <summary>
    ///     Creates sample plugin segment data for testing.
    /// </summary>
    public static List<string> CreatePluginSegment(params string[] plugins)
    {
        if (plugins.Length == 0)
        {
            plugins = new[]
            {
                "[00] Fallout4.esm",
                "[01] DLCRobot.esm",
                "[FE:001] TestMod.esp"
            };
        }
        
        return plugins.ToList();
    }

    /// <summary>
    ///     Creates sample call stack segment data for testing.
    /// </summary>
    public static List<string> CreateCallStackSegment(params string[] lines)
    {
        if (lines.Length == 0)
        {
            lines = new[]
            {
                "[0] 0x7FF6B1234567 Fallout4.exe+0x1234567",
                "[1] 0x7FF6B1234568 nvwgf2umx.dll+0x123",
                "[2] 0x7FF6B1234569 KERNEL32.DLL+0x456"
            };
        }
        
        return lines.ToList();
    }

    /// <summary>
    ///     Creates a mock IModDatabase with sample data.
    /// </summary>
    public static IModDatabase CreateModDatabase(
        Dictionary<string, Dictionary<string, string>>? warningCategories = null,
        Action<IModDatabase>? configure = null)
    {
        var mock = Substitute.For<IModDatabase>();
        
        warningCategories ??= new Dictionary<string, Dictionary<string, string>>
        {
            ["FREQ"] = new Dictionary<string, string>
            {
                ["ScrapEverything"] = "Can cause crashes with workshop items",
                ["SpringCleaning"] = "May conflict with precombines"
            },
            ["WARN"] = new Dictionary<string, string>
            {
                ["UnofficalPatch"] = "Requires specific load order position"
            }
        };
        
        foreach (var (category, warnings) in warningCategories)
        {
            mock.LoadModWarningsAsync(category, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(warnings));
        }
        
        configure?.Invoke(mock);
        
        return mock;
    }
}