using Scanner111.Core.Models;

namespace Scanner111.Test.Infrastructure.TestBuilders;

/// <summary>
///     Builder for creating ModDetectionSettings test data.
/// </summary>
public class ModDetectionSettingsBuilder
{
    private HashSet<string> _xseModules = new();
    private bool _hasXCell = false;
    private bool _hasBakaScrapHeap = false;
    private List<string> _detectedMods = new();
    private Dictionary<string, string> _modVersions = new();

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
        _detectedMods.Add("BakaScrapHeap");
        return this;
    }

    public ModDetectionSettingsBuilder WithDetectedMod(string modName, string? version = null)
    {
        _detectedMods.Add(modName);
        if (version != null)
            _modVersions[modName] = version;
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
        var settings = new ModDetectionSettings
        {
            XseModules = _xseModules,
            HasXCell = _hasXCell,
            HasBakaScrapHeap = _hasBakaScrapHeap
        };

        // Add detected mods
        foreach (var mod in _detectedMods)
        {
            settings.DetectedMods?.Add(mod);
        }

        // Add mod versions
        foreach (var kvp in _modVersions)
        {
            settings.ModVersions?.Add(kvp.Key, kvp.Value);
        }

        return settings;
    }

    public static implicit operator ModDetectionSettings(ModDetectionSettingsBuilder builder)
        => builder.Build();
}