using Scanner111.Core.Models;

namespace Scanner111.Test.Infrastructure.TestBuilders;

/// <summary>
///     Builder for creating ModDetectionSettings test data.
/// </summary>
public class ModDetectionSettingsBuilder
{
    private HashSet<string> _xseModules = new();
    private bool _hasXCell = false;
    private bool _hasOldXCell = false;
    private bool _hasBakaScrapHeap = false;
    private Dictionary<string, string> _crashLogPlugins = new();
    private bool? _fcxMode = null;
    private Dictionary<string, object> _metadata = new();
    private List<ModWarning> _detectedWarnings = new();
    private List<ModConflict> _detectedConflicts = new();
    private List<ImportantMod> _importantMods = new();
    private string? _detectedGpuType = null;

    public static ModDetectionSettingsBuilder Create() => new();

    public ModDetectionSettingsBuilder WithXseModule(string module)
    {
        _xseModules.Add(module);
        return this;
    }

    public ModDetectionSettingsBuilder WithXseModules(params string[] modules)
    {
        foreach (var module in modules)
            _xseModules.Add(module);
        return this;
    }

    public ModDetectionSettingsBuilder WithF4EE()
    {
        _xseModules.Add("f4ee.dll");
        return this;
    }

    public ModDetectionSettingsBuilder WithAchievements()
    {
        _xseModules.Add("achievements.dll");
        return this;
    }

    public ModDetectionSettingsBuilder WithXCell()
    {
        _hasXCell = true;
        _xseModules.Add("x-cell-ng2.dll");
        return this;
    }

    public ModDetectionSettingsBuilder WithBakaScrapHeap()
    {
        _hasBakaScrapHeap = true;
        _xseModules.Add("bakascrapheap.dll");
        return this;
    }

    public ModDetectionSettingsBuilder WithCrashLogPlugin(string pluginName, string info = "")
    {
        _crashLogPlugins[pluginName] = info;
        return this;
    }
    
    public ModDetectionSettingsBuilder WithFcxMode(bool fcxMode)
    {
        _fcxMode = fcxMode;
        return this;
    }

    public ModDetectionSettingsBuilder WithMemoryConflict()
    {
        // Setup conflicting memory management mods
        WithXCell();
        WithBakaScrapHeap();
        return this;
    }

    public ModDetectionSettingsBuilder WithDefaultSetup()
    {
        WithF4EE();
        _hasXCell = false;
        _hasBakaScrapHeap = false;
        return this;
    }

    public ModDetectionSettings Build()
    {
        return ModDetectionSettings.FromDetectionData(
            xseModules: _xseModules,
            crashLogPlugins: _crashLogPlugins,
            fcxMode: _fcxMode,
            metadata: _metadata,
            detectedWarnings: _detectedWarnings,
            detectedConflicts: _detectedConflicts,
            importantMods: _importantMods,
            detectedGpuType: _detectedGpuType);
    }

    public static implicit operator ModDetectionSettings(ModDetectionSettingsBuilder builder)
        => builder.Build();
}