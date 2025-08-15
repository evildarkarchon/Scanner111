using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
///     Base class for analyzer unit tests that provides common setup and utilities.
///     Reduces duplication across analyzer test classes.
/// </summary>
/// <typeparam name="TAnalyzer">The type of analyzer being tested</typeparam>
public abstract class AnalyzerTestBase<TAnalyzer> where TAnalyzer : IAnalyzer
{
    protected readonly TAnalyzer Analyzer;
    protected readonly TestApplicationSettingsService AppSettings;
    protected readonly TestFormIdDatabaseService FormIdDatabase;
    protected readonly TestHashValidationService HashService;
    protected readonly TestYamlSettingsProvider YamlSettings;

    protected AnalyzerTestBase()
    {
        YamlSettings = new TestYamlSettingsProvider();
        AppSettings = new TestApplicationSettingsService();
        FormIdDatabase = new TestFormIdDatabaseService();
        HashService = new TestHashValidationService();

        Analyzer = CreateAnalyzer();
    }

    /// <summary>
    ///     Factory method to create the analyzer instance.
    ///     Override this in derived classes to provide specific analyzer construction.
    /// </summary>
    protected abstract TAnalyzer CreateAnalyzer();

    /// <summary>
    ///     Creates a basic crash log for testing with default values.
    /// </summary>
    protected CrashLog CreateTestCrashLog(
        string filePath = "test.log",
        List<string>? callStack = null,
        Dictionary<string, string>? plugins = null)
    {
        return new CrashLog
        {
            FilePath = filePath,
            CallStack = callStack ?? new List<string>(),
            Plugins = plugins ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    ///     Creates a crash log with sample plugin data.
    /// </summary>
    protected CrashLog CreateCrashLogWithPlugins(params string[] pluginNames)
    {
        var plugins = new Dictionary<string, string>();
        for (var i = 0; i < pluginNames.Length; i++) plugins[pluginNames[i]] = i.ToString("X2");

        return new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>(),
            Plugins = plugins
        };
    }

    /// <summary>
    ///     Creates a crash log with sample call stack entries.
    /// </summary>
    protected CrashLog CreateCrashLogWithCallStack(params string[] stackLines)
    {
        return new CrashLog
        {
            FilePath = "test.log",
            CallStack = stackLines.ToList(),
            Plugins = new Dictionary<string, string>()
        };
    }

    /// <summary>
    ///     Configures application settings for testing.
    /// </summary>
    protected async Task ConfigureSettingsAsync(Action<ApplicationSettings> configure)
    {
        var settings = new ApplicationSettings();
        configure(settings);
        await AppSettings.SaveSettingsAsync(settings);
    }

    /// <summary>
    ///     Gets a null logger instance for the analyzer type.
    /// </summary>
    protected static ILogger<T> GetNullLogger<T>()
    {
        return NullLogger<T>.Instance;
    }
}