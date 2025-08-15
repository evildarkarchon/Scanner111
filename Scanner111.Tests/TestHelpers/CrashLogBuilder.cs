using Scanner111.Core.Models;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
///     Fluent builder for creating CrashLog instances in tests.
///     Reduces boilerplate code and improves test readability.
/// </summary>
public class CrashLogBuilder
{
    private readonly List<string> _callStack = new();
    private readonly Dictionary<string, object> _crashgenSettings = new();
    private readonly List<string> _originalLines = new();
    private readonly Dictionary<string, string> _plugins = new();
    private readonly HashSet<string> _xseModules = new();
    private string _crashGenVersion = "4.26.0";
    private DateTime? _crashTime = DateTime.UtcNow;
    private readonly string _documentsPath = string.Empty;
    private readonly string _executableHash = string.Empty;
    private string _filePath = "test.log";
    private string _gamePath = string.Empty;
    private string _gamePlatform = "Steam";
    private string _gameType = "Fallout4";
    private string _gameVersion = "1.10.163";
    private bool _isIncomplete;
    private string _mainError = string.Empty;
    private string _xseVersion = string.Empty;

    /// <summary>
    ///     Sets the crash log file path.
    /// </summary>
    public CrashLogBuilder WithFilePath(string filePath)
    {
        _filePath = filePath;
        return this;
    }

    /// <summary>
    ///     Sets the crash time.
    /// </summary>
    public CrashLogBuilder WithCrashTime(DateTime? crashTime)
    {
        _crashTime = crashTime;
        return this;
    }

    /// <summary>
    ///     Sets the game version.
    /// </summary>
    public CrashLogBuilder WithGameVersion(string version)
    {
        _gameVersion = version;
        return this;
    }

    /// <summary>
    ///     Sets the crash generator version.
    /// </summary>
    public CrashLogBuilder WithCrashGenVersion(string version)
    {
        _crashGenVersion = version;
        return this;
    }

    /// <summary>
    ///     Adds call stack entries.
    /// </summary>
    public CrashLogBuilder WithCallStack(params string[] stackLines)
    {
        _callStack.AddRange(stackLines);
        return this;
    }

    /// <summary>
    ///     Adds a single plugin.
    /// </summary>
    public CrashLogBuilder WithPlugin(string name, string index)
    {
        _plugins[name] = index;
        return this;
    }

    /// <summary>
    ///     Adds multiple plugins.
    /// </summary>
    public CrashLogBuilder WithPlugins(Dictionary<string, string> plugins)
    {
        foreach (var kvp in plugins)
            _plugins[kvp.Key] = kvp.Value;
        return this;
    }

    /// <summary>
    ///     Adds plugins by name, automatically assigning indices.
    /// </summary>
    public CrashLogBuilder WithPlugins(params string[] pluginNames)
    {
        for (var i = 0; i < pluginNames.Length; i++)
            _plugins[pluginNames[i]] = i.ToString("X2");
        return this;
    }

    /// <summary>
    ///     Sets the main error message.
    /// </summary>
    public CrashLogBuilder WithMainError(string mainError)
    {
        _mainError = mainError;
        return this;
    }

    /// <summary>
    ///     Adds original log lines.
    /// </summary>
    public CrashLogBuilder WithOriginalLines(params string[] lines)
    {
        _originalLines.AddRange(lines);
        return this;
    }

    /// <summary>
    ///     Adds XSE modules.
    /// </summary>
    public CrashLogBuilder WithXseModules(params string[] modules)
    {
        foreach (var module in modules)
            _xseModules.Add(module);
        return this;
    }

    /// <summary>
    ///     Adds crash generator settings.
    /// </summary>
    public CrashLogBuilder WithCrashgenSettings(Dictionary<string, object> settings)
    {
        foreach (var kvp in settings)
            _crashgenSettings[kvp.Key] = kvp.Value;
        return this;
    }

    /// <summary>
    ///     Sets whether the log is incomplete.
    /// </summary>
    public CrashLogBuilder AsIncomplete(bool isIncomplete = true)
    {
        _isIncomplete = isIncomplete;
        return this;
    }

    /// <summary>
    ///     Sets the game type.
    /// </summary>
    public CrashLogBuilder WithGameType(string gameType)
    {
        _gameType = gameType;
        return this;
    }

    /// <summary>
    ///     Sets the game path.
    /// </summary>
    public CrashLogBuilder WithGamePath(string gamePath)
    {
        _gamePath = gamePath;
        return this;
    }

    /// <summary>
    ///     Sets the game platform.
    /// </summary>
    public CrashLogBuilder WithGamePlatform(string gamePlatform)
    {
        _gamePlatform = gamePlatform;
        return this;
    }

    /// <summary>
    ///     Sets the XSE version.
    /// </summary>
    public CrashLogBuilder WithXseVersion(string xseVersion)
    {
        _xseVersion = xseVersion;
        return this;
    }

    /// <summary>
    ///     Creates a minimal crash log for quick testing.
    /// </summary>
    public static CrashLogBuilder Minimal()
    {
        return new CrashLogBuilder()
            .WithFilePath("minimal.log")
            .WithCallStack("Test stack line");
    }

    /// <summary>
    ///     Creates a typical crash log with common test data.
    /// </summary>
    public static CrashLogBuilder Typical()
    {
        return new CrashLogBuilder()
            .WithFilePath("typical.log")
            .WithGameVersion("1.10.163")
            .WithCrashGenVersion("4.26.0")
            .WithPlugins("Fallout4.esm", "DLCRobot.esm", "DLCworkshop01.esm")
            .WithCallStack(
                "  [0] 0x7FF6F1234567",
                "  [1] 0x7FF6F1234568",
                "  [2] 0x7FF6F1234569")
            .WithMainError("Unhandled exception at 0x7FF6F1234567");
    }

    /// <summary>
    ///     Creates a crash log with form IDs for testing FormIdAnalyzer.
    /// </summary>
    public static CrashLogBuilder WithFormIds(params string[] formIds)
    {
        var builder = new CrashLogBuilder()
            .WithFilePath("formid-test.log");

        var stackLines = new List<string>();
        foreach (var formId in formIds) stackLines.Add($"  Form ID: {formId}");

        return builder.WithCallStack(stackLines.ToArray());
    }

    /// <summary>
    ///     Builds the CrashLog instance.
    /// </summary>
    public CrashLog Build()
    {
        return new CrashLog
        {
            FilePath = _filePath,
            OriginalLines = _originalLines,
            MainError = _mainError,
            CallStack = _callStack,
            Plugins = _plugins,
            XseModules = _xseModules,
            CrashgenSettings = _crashgenSettings,
            CrashGenVersion = _crashGenVersion,
            CrashTime = _crashTime,
            GameVersion = _gameVersion,
            IsIncomplete = _isIncomplete,
            GameType = _gameType,
            GamePath = _gamePath,
            GamePlatform = _gamePlatform,
            XseVersion = _xseVersion,
            DocumentsPath = _documentsPath,
            ExecutableHash = _executableHash
        };
    }

    /// <summary>
    ///     Implicit conversion to CrashLog for convenience.
    /// </summary>
    public static implicit operator CrashLog(CrashLogBuilder builder)
    {
        return builder.Build();
    }
}