using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using System.Collections.Concurrent;

namespace Scanner111.Test.Infrastructure.Mocks;

/// <summary>
/// Centralized factory for creating mock objects with sensible defaults.
/// Eliminates 500+ lines of duplicate mock setup code across test files.
/// </summary>
public static class MockFactory
{
    private static readonly ConcurrentDictionary<Type, object> _defaultMocks = new();

    /// <summary>
    /// Creates a mock IAsyncYamlSettingsCore with default settings.
    /// </summary>
    public static IAsyncYamlSettingsCore CreateYamlCore(Dictionary<string, object>? customSettings = null)
    {
        var mock = Substitute.For<IAsyncYamlSettingsCore>();
        
        // Setup default responses for common types
        mock.GetSettingAsync<List<string>>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<List<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));
        
        mock.GetSettingAsync<Dictionary<string, string>>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Dictionary<string, string>?>(new Dictionary<string, string>()));
        
        mock.GetSettingAsync<string>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
        
        mock.GetSettingAsync<bool>(
                Arg.Any<YamlStore>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        
        // Apply custom settings if provided
        if (customSettings != null)
        {
            foreach (var (key, value) in customSettings)
            {
                mock.GetSettingAsync(
                        Arg.Any<YamlStore>(),
                        key,
                        Arg.Any<object?>(),
                        Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<object?>(value));
            }
        }
        
        return mock;
    }

    /// <summary>
    /// Creates a mock IPluginLoader with optional plugins.
    /// </summary>
    public static IPluginLoader CreatePluginLoader(
        Dictionary<string, string>? plugins = null,
        bool pluginsLoaded = true,
        bool hasLoadOrder = false)
    {
        var mock = Substitute.For<IPluginLoader>();
        var pluginDict = plugins ?? new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" },
            { "Update.esm", "01" },
            { "TestMod.esp", "02" }
        };
        
        // Mock LoadFromLoadOrderFileAsync
        mock.LoadFromLoadOrderFileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((
                plugins: hasLoadOrder ? pluginDict : new Dictionary<string, string>(),
                pluginsLoaded: hasLoadOrder && pluginsLoaded,
                fragment: ReportFragment.CreateInfo(
                    hasLoadOrder ? "Load Order" : "No Load Order",
                    hasLoadOrder ? "Plugins loaded from loadorder.txt" : "No load order file found"))));
        
        // Mock ScanPluginsFromLog
        mock.ScanPluginsFromLog(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<Version>(),
            Arg.Any<Version>(),
            Arg.Any<ISet<string>?>())
            .Returns((
                plugins: pluginDict,
                limitTriggered: false,
                limitCheckDisabled: false));
        
        // Mock CreatePluginInfoCollection
        var pluginInfoList = pluginDict.Select((kvp, idx) => new PluginInfo
        {
            Name = kvp.Key,
            Origin = kvp.Value,
            Index = idx,
            IsIgnored = false
        }).ToList();
        
        mock.CreatePluginInfoCollection(
            Arg.Any<IDictionary<string, string>?>(),
            Arg.Any<IDictionary<string, string>?>(),
            Arg.Any<ISet<string>?>())
            .Returns(pluginInfoList);
        
        // Mock FilterIgnoredPlugins
        mock.FilterIgnoredPlugins(
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<ISet<string>>())
            .Returns(callInfo =>
            {
                var inputPlugins = callInfo.ArgAt<IDictionary<string, string>>(0);
                var ignoredPlugins = callInfo.ArgAt<ISet<string>>(1);
                return inputPlugins.Where(kvp => !ignoredPlugins.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            });
        
        // Mock ValidateLoadOrderFileAsync
        mock.ValidateLoadOrderFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(hasLoadOrder));
        
        // Mock GetStatistics
        mock.GetStatistics()
            .Returns(new PluginLoadingStatistics
            {
                LoadOrderPluginCount = hasLoadOrder ? pluginDict.Count : 0,
                CrashLogPluginCount = !hasLoadOrder ? pluginDict.Count : 0,
                IgnoredPluginCount = 0,
                PluginLimitTriggered = false,
                LimitCheckDisabled = false,
                LastOperationDuration = TimeSpan.FromMilliseconds(100)
            });
        
        return mock;
    }

    /// <summary>
    /// Creates a mock IModDatabase with optional mod data.
    /// </summary>
    public static IModDatabase CreateModDatabase(
        Dictionary<string, string>? warnings = null,
        Dictionary<string, string>? conflicts = null)
    {
        var mock = Substitute.For<IModDatabase>();
        
        // Default warnings data
        var modWarnings = warnings ?? new Dictionary<string, string>
        {
            { "UnstableMod.esp", "This mod is known to cause crashes" },
            { "ProblematicMod.esp", "Performance issues reported" }
        };
        
        // Default conflicts data
        var modConflicts = conflicts ?? new Dictionary<string, string>
        {
            { "ModA.esp|ModB.esp", "These mods conflict with each other" }
        };
        
        mock.LoadModWarningsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(modWarnings));
        
        mock.LoadModConflictsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(modConflicts));
        
        mock.LoadImportantModsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>()));
        
        mock.GetModWarningCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new List<string> { "FREQ", "PERF", "STAB" }));
        
        mock.GetImportantModCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new List<string> { "CORE", "CORE_FOLON" }));
        
        return mock;
    }

    /// <summary>
    /// Creates a mock IXsePluginChecker.
    /// </summary>
    public static IXsePluginChecker CreateXsePluginChecker(bool hasXsePlugin = false)
    {
        var mock = Substitute.For<IXsePluginChecker>();
        // Configure based on actual interface methods once verified
        return mock;
    }

    /// <summary>
    /// Creates a mock ICrashGenChecker.
    /// </summary>
    public static ICrashGenChecker CreateCrashGenChecker(bool hasCrashGen = false)
    {
        var mock = Substitute.For<ICrashGenChecker>();
        // Configure based on actual interface methods once verified
        return mock;
    }

    /// <summary>
    /// Creates a mock IMessageService.
    /// </summary>
    public static IMessageService CreateMessageService()
    {
        var mock = Substitute.For<IMessageService>();
        // Configure based on actual interface methods once verified
        return mock;
    }

    /// <summary>
    /// Creates a mock IGpuDetector.
    /// </summary>
    public static IGpuDetector CreateGpuDetector()
    {
        var mock = Substitute.For<IGpuDetector>();
        // Configure based on actual interface methods once verified
        return mock;
    }

    /// <summary>
    /// Creates a mock IModFileScanner.
    /// </summary>
    public static IModFileScanner CreateModFileScanner()
    {
        var mock = Substitute.For<IModFileScanner>();
        // Configure based on actual interface methods once verified
        return mock;
    }

    /// <summary>
    /// Creates a mock logger of any type.
    /// </summary>
    public static ILogger<T> CreateLogger<T>()
    {
        return Substitute.For<ILogger<T>>();
    }

    /// <summary>
    /// Creates an AnalysisContext with mock YAML core.
    /// </summary>
    public static AnalysisContext CreateContext(
        string logPath = @"C:\test\crashlog.txt",
        Dictionary<string, object>? yamlSettings = null)
    {
        var yamlCore = CreateYamlCore(yamlSettings);
        return new AnalysisContext(logPath, yamlCore);
    }

    /// <summary>
    /// Creates a context with pre-populated shared data.
    /// </summary>
    public static AnalysisContext CreateContextWithData(
        params (string Key, object Value)[] sharedData)
    {
        var context = CreateContext();
        foreach (var (key, value) in sharedData)
        {
            context.SetSharedData(key, value);
        }
        return context;
    }

    /// <summary>
    /// Creates a plugin segment for testing.
    /// </summary>
    public static string CreatePluginSegment(params string[] plugins)
    {
        if (plugins.Length == 0)
        {
            plugins = new[] { "[00] Skyrim.esm", "[01] Update.esm", "[0A 10] TestMod.esp" };
        }
        
        return "PLUGINS:\n" + string.Join("\n", plugins.Select(p => $"    {p}"));
    }

    /// <summary>
    /// Creates a call stack segment for testing.
    /// </summary>
    public static string CreateCallStackSegment(params string[] frames)
    {
        if (frames.Length == 0)
        {
            frames = new[]
            {
                "[0] SkyrimSE.exe+0x123456",
                "[1] SkyrimSE.exe+0x234567",
                "[2] ntdll.dll+0x345678"
            };
        }
        
        return "CALL STACK:\n" + string.Join("\n", frames.Select(f => $"  {f}"));
    }

    private static List<PluginInfo> CreateDefaultPlugins()
    {
        return new List<PluginInfo>
        {
            new() { Name = "Skyrim.esm", Origin = "00", Index = 0 },
            new() { Name = "Update.esm", Origin = "01", Index = 1 },
            new() { Name = "Dawnguard.esm", Origin = "02", Index = 2 },
            new() { Name = "HearthFires.esm", Origin = "03", Index = 3 },
            new() { Name = "Dragonborn.esm", Origin = "04", Index = 4 }
        };
    }
}