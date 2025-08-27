using System.Collections.Concurrent;
using Scanner111.Core.Configuration;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Provides context and shared resources for analyzer execution.
///     Thread-safe for concurrent analyzer access.
/// </summary>
public sealed class AnalysisContext
{
    private readonly ConcurrentDictionary<string, object> _metadata;
    private readonly ConcurrentDictionary<string, object> _sharedData;

    public AnalysisContext(
        string inputPath,
        IAsyncYamlSettingsCore yamlCore,
        AnalysisType analysisType = AnalysisType.CrashLog)
    {
        InputPath = inputPath ?? throw new ArgumentNullException(nameof(inputPath));
        YamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        AnalysisType = analysisType;

        _sharedData = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        _metadata = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        InputFileName = Path.GetFileName(inputPath);
        InputDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        StartTime = DateTime.UtcNow;
        CorrelationId = Guid.NewGuid();
    }

    /// <summary>
    ///     Gets the full path to the input file or directory being analyzed.
    /// </summary>
    public string InputPath { get; }

    /// <summary>
    ///     Gets the input file name without path.
    /// </summary>
    public string InputFileName { get; }

    /// <summary>
    ///     Gets the input directory path.
    /// </summary>
    public string InputDirectory { get; }

    /// <summary>
    ///     Gets the type of analysis being performed.
    /// </summary>
    public AnalysisType AnalysisType { get; }

    /// <summary>
    ///     Gets the YAML settings core for accessing configuration.
    /// </summary>
    public IAsyncYamlSettingsCore YamlCore { get; }

    /// <summary>
    ///     Gets the time when the analysis started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    ///     Gets the unique correlation ID for this analysis run.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    ///     Gets thread-safe access to shared data between analyzers.
    /// </summary>
    public IReadOnlyDictionary<string, object> SharedData => _sharedData;

    /// <summary>
    ///     Gets thread-safe access to analysis metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>
    ///     Adds or updates a shared data value. Thread-safe.
    /// </summary>
    public void SetSharedData(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        _sharedData.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    ///     Tries to get a shared data value. Thread-safe.
    /// </summary>
    public bool TryGetSharedData<T>(string key, out T? value)
    {
        if (_sharedData.TryGetValue(key, out var objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Adds or updates a metadata value. Thread-safe.
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        _metadata.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    ///     Creates a child context with the same shared resources but different input.
    /// </summary>
    public AnalysisContext CreateChildContext(string newInputPath)
    {
        var childContext = new AnalysisContext(newInputPath, YamlCore, AnalysisType);

        // Copy shared data to child context
        foreach (var kvp in _sharedData) childContext._sharedData.TryAdd(kvp.Key, kvp.Value);

        // Link correlation IDs
        childContext.SetMetadata("ParentCorrelationId", CorrelationId);

        return childContext;
    }
}

/// <summary>
///     Specifies the type of analysis being performed.
/// </summary>
public enum AnalysisType
{
    /// <summary>
    ///     Analysis of crash log files.
    /// </summary>
    CrashLog,

    /// <summary>
    ///     Analysis of game integrity.
    /// </summary>
    GameIntegrity,

    /// <summary>
    ///     Analysis of mod configuration.
    /// </summary>
    ModConfiguration,

    /// <summary>
    ///     Combined analysis of multiple aspects.
    /// </summary>
    Combined
}