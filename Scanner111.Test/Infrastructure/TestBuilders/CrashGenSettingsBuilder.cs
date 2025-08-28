using Scanner111.Core.Models;

namespace Scanner111.Test.Infrastructure.TestBuilders;

/// <summary>
///     Builder for creating CrashGenSettings test data.
/// </summary>
public class CrashGenSettingsBuilder
{
    private string _crashGenName = "Buffout";
    private Version _version = new(1, 30, 0);
    private bool _achievements = false;
    private bool _memoryManager = true;
    private bool _archiveLimit = false;
    private bool _f4ee = true;
    private bool _havokMemorySystem = false;
    private Dictionary<string, object> _rawSettings = new();
    private HashSet<string> _ignoredSettings = new();

    public static CrashGenSettingsBuilder Create() => new();

    public CrashGenSettingsBuilder WithCrashGenName(string name)
    {
        _crashGenName = name;
        return this;
    }

    public CrashGenSettingsBuilder WithVersion(Version version)
    {
        _version = version;
        return this;
    }

    public CrashGenSettingsBuilder WithVersion(int major, int minor, int patch = 0)
    {
        _version = new Version(major, minor, patch);
        return this;
    }

    public CrashGenSettingsBuilder WithAchievements(bool enabled = true)
    {
        _achievements = enabled;
        return this;
    }

    public CrashGenSettingsBuilder WithMemoryManager(bool enabled = true)
    {
        _memoryManager = enabled;
        return this;
    }

    public CrashGenSettingsBuilder WithArchiveLimit(bool enabled = true)
    {
        _archiveLimit = enabled;
        return this;
    }

    public CrashGenSettingsBuilder WithF4EE(bool enabled = true)
    {
        _f4ee = enabled;
        return this;
    }

    public CrashGenSettingsBuilder WithHavokMemorySystem(bool enabled = true)
    {
        _havokMemorySystem = enabled;
        return this;
    }

    public CrashGenSettingsBuilder WithRawSetting(string key, object value)
    {
        _rawSettings[key] = value;
        return this;
    }

    public CrashGenSettingsBuilder WithRawSettings(Dictionary<string, object> settings)
    {
        _rawSettings = settings;
        return this;
    }

    public CrashGenSettingsBuilder WithIgnoredSetting(string setting)
    {
        _ignoredSettings.Add(setting);
        return this;
    }

    public CrashGenSettingsBuilder WithConflictingMemorySettings()
    {
        _memoryManager = true;
        _havokMemorySystem = true;
        return this;
    }

    public CrashGenSettingsBuilder WithOldVersionSettings()
    {
        _version = new Version(1, 28, 0);
        _archiveLimit = true;
        return this;
    }

    public CrashGenSettingsBuilder WithDefaultBuffout4Settings()
    {
        _crashGenName = "Buffout";
        _version = new Version(1, 30, 0);
        _achievements = false;
        _memoryManager = true;
        _archiveLimit = false;
        _f4ee = true;
        _havokMemorySystem = false;
        return this;
    }

    public CrashGenSettings Build()
    {
        return new CrashGenSettings
        {
            CrashGenName = _crashGenName,
            Version = _version,
            Achievements = _achievements,
            MemoryManager = _memoryManager,
            ArchiveLimit = _archiveLimit,
            F4EE = _f4ee,
            HavokMemorySystem = _havokMemorySystem,
            RawSettings = _rawSettings,
            IgnoredSettings = _ignoredSettings
        };
    }

    public static implicit operator CrashGenSettings(CrashGenSettingsBuilder builder)
        => builder.Build();
}