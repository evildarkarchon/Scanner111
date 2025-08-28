using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Test.Infrastructure.TestData;
using Scanner111.Test.Infrastructure.Snapshots;
using VerifyXunit;

namespace Scanner111.Test.Infrastructure;

/// <summary>
///     Base class for tests using embedded resources and synthetic data.
///     Provides self-contained testing without external dependencies.
/// </summary>
public abstract class EmbeddedResourceTestBase : SnapshotTestBase
{
    private EmbeddedResourceProvider _resourceProvider = null!;
    private CrashLogDataGenerator _dataGenerator = null!;
    
    protected EmbeddedResourceProvider ResourceProvider => _resourceProvider;
    protected CrashLogDataGenerator DataGenerator => _dataGenerator;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Register test data services
        services.AddSingleton<EmbeddedResourceProvider>();
        services.AddTransient<CrashLogDataGenerator>();
    }

    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync().ConfigureAwait(false);
        
        _resourceProvider = GetService<EmbeddedResourceProvider>();
        _dataGenerator = GetService<CrashLogDataGenerator>();
        
        // Optionally preload resources for faster access
        if (ShouldPreloadResources())
        {
            await _resourceProvider.PreloadAllResourcesAsync().ConfigureAwait(false);
        }
    }

    protected override async Task OnDisposeAsync()
    {
        if (_resourceProvider != null)
        {
            await _resourceProvider.ClearCacheAsync().ConfigureAwait(false);
        }
        await base.OnDisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets an embedded crash log by name.
    /// </summary>
    protected Task<string> GetEmbeddedLogAsync(string logName, CancellationToken cancellationToken = default)
    {
        return _resourceProvider.GetEmbeddedLogAsync(logName, cancellationToken);
    }

    /// <summary>
    ///     Gets all available embedded log names.
    /// </summary>
    protected IEnumerable<string> GetAvailableEmbeddedLogs()
    {
        return _resourceProvider.GetAvailableEmbeddedLogs();
    }

    /// <summary>
    ///     Creates a test file from embedded resource.
    /// </summary>
    protected async Task<string> CreateTestFileFromEmbeddedAsync(
        string resourceName, 
        CancellationToken cancellationToken = default)
    {
        return await _resourceProvider.WriteToTempFileAsync(
            resourceName, 
            TestDirectory, 
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Generates synthetic crash log data.
    /// </summary>
    protected string GenerateSyntheticCrashLog(CrashLogOptions? options = null)
    {
        return _dataGenerator.GenerateCrashLog(options);
    }

    /// <summary>
    ///     Creates a test file with synthetic crash log data.
    /// </summary>
    protected async Task<string> CreateSyntheticCrashLogFileAsync(
        string fileName,
        CrashLogOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var content = _dataGenerator.GenerateCrashLog(options);
        var filePath = Path.Combine(TestDirectory, fileName);
        
        await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
        Logger.LogDebug("Created synthetic crash log file: {FilePath}", filePath);
        
        return filePath;
    }

    /// <summary>
    ///     Gets embedded expected output for validation.
    /// </summary>
    protected Task<string?> GetEmbeddedExpectedOutputAsync(
        string crashLogName,
        CancellationToken cancellationToken = default)
    {
        return _resourceProvider.GetEmbeddedExpectedOutputAsync(crashLogName, cancellationToken);
    }

    /// <summary>
    ///     Creates a deterministic synthetic crash log for consistent testing.
    /// </summary>
    protected string GenerateDeterministicCrashLog(int seed, CrashLogOptions? options = null)
    {
        var deterministicGenerator = new CrashLogDataGenerator(seed);
        return deterministicGenerator.GenerateCrashLog(options);
    }

    /// <summary>
    ///     Override to control resource preloading behavior.
    /// </summary>
    protected virtual bool ShouldPreloadResources() => false;

    /// <summary>
    ///     Combines embedded and synthetic data for comprehensive testing.
    /// </summary>
    protected async Task<string> CreateHybridTestFileAsync(
        string baseResourceName,
        Action<CrashLogOptions> customizeOptions,
        CancellationToken cancellationToken = default)
    {
        // Start with real embedded data
        var baseContent = await GetEmbeddedLogAsync(baseResourceName, cancellationToken)
            .ConfigureAwait(false);
        
        // Parse and extract settings from real log
        var options = ExtractOptionsFromLog(baseContent);
        
        // Apply customizations
        customizeOptions(options);
        
        // Generate modified synthetic version
        var modifiedContent = _dataGenerator.GenerateCrashLog(options);
        
        // Write to test file
        var fileName = $"hybrid_{baseResourceName}";
        var filePath = Path.Combine(TestDirectory, fileName);
        await File.WriteAllTextAsync(filePath, modifiedContent, cancellationToken)
            .ConfigureAwait(false);
        
        return filePath;
    }

    private CrashLogOptions ExtractOptionsFromLog(string logContent)
    {
        var options = new CrashLogOptions();
        
        // Extract version information
        var versionMatch = System.Text.RegularExpressions.Regex.Match(
            logContent, @"Buffout 4 v([\d\.]+)");
        if (versionMatch.Success)
        {
            options.BuffoutVersion = versionMatch.Groups[1].Value;
        }
        
        // Extract error type
        var errorMatch = System.Text.RegularExpressions.Regex.Match(
            logContent, @"Unhandled exception ""([^""]+)""");
        if (errorMatch.Success)
        {
            options.ErrorType = errorMatch.Groups[1].Value;
        }
        
        // Extract plugin count
        var pluginMatch = System.Text.RegularExpressions.Regex.Match(
            logContent, @"PLUGINS \((\d+)\)");
        if (pluginMatch.Success && int.TryParse(pluginMatch.Groups[1].Value, out var count))
        {
            options.PluginCount = count;
        }
        
        return options;
    }
}

/// <summary>
///     Theory data for embedded resource tests.
/// </summary>
public class EmbeddedLogTheoryData : TheoryData<string>
{
    public EmbeddedLogTheoryData()
    {
        // Add all critical samples
        foreach (var sample in CriticalSampleLogs.GetAllCriticalSamples())
        {
            Add(sample);
        }
    }
}

/// <summary>
///     Theory data for synthetic test scenarios.
/// </summary>
public class SyntheticScenarioTheoryData : TheoryData<int, CrashLogOptions>
{
    public SyntheticScenarioTheoryData()
    {
        // Access violation with plugin issues
        Add(1, new CrashLogOptions
        {
            ErrorType = "EXCEPTION_ACCESS_VIOLATION",
            ProblematicPlugins = new[] { "BrokenMod.esp", "Incompatible.esm" }
        });
        
        // Stack overflow
        Add(2, new CrashLogOptions
        {
            ErrorType = "EXCEPTION_STACK_OVERFLOW",
            CallstackDepth = 50
        });
        
        // Memory issues
        Add(3, new CrashLogOptions
        {
            Settings = new Scanner111.Core.Models.CrashGenSettings
            {
                MemoryManager = false,
                ArchiveLimit = false
            }
        });
        
        // Large plugin list
        Add(4, new CrashLogOptions
        {
            PluginCount = 255
        });
        
        // Minimal crash
        Add(5, new CrashLogOptions
        {
            PluginCount = 10,
            CallstackDepth = 3,
            IncludeSystemSpecs = false,
            IncludeStack = false
        });
    }
}