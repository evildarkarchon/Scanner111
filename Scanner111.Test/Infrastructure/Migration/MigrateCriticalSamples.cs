using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Test.Infrastructure.TestData;
using Xunit.Abstractions;

namespace Scanner111.Test.Infrastructure.Migration;

/// <summary>
///     Test runner to migrate critical sample logs to embedded resources.
///     This is part of Q3: Sample Removal phase of the test suite audit.
/// </summary>
public class MigrateCriticalSamples
{
    private readonly ITestOutputHelper _output;
    private readonly TestMigrationHelper _migrationHelper;

    public MigrateCriticalSamples(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(output));
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<TestMigrationHelper>>();
        _migrationHelper = new TestMigrationHelper(logger);
    }

    [Fact]
    public async Task MigrateCriticalSampleLogsToEmbeddedResources()
    {
        // Execute batch migration of critical samples
        var result = await _migrationHelper.MigrateCriticalSamplesAsync();
        
        // Report results
        _output.WriteLine($"Migration Results:");
        _output.WriteLine($"  Total Attempted: {result.TotalAttempted}");
        _output.WriteLine($"  Successful: {result.SuccessfulMigrations.Count}");
        _output.WriteLine($"  Failed: {result.FailedMigrations.Count}");
        _output.WriteLine($"  Success Rate: {result.SuccessRate:P}");
        
        if (result.SuccessfulMigrations.Any())
        {
            _output.WriteLine("\nSuccessfully Migrated:");
            foreach (var file in result.SuccessfulMigrations)
            {
                _output.WriteLine($"  ✓ {file}");
            }
        }
        
        if (result.FailedMigrations.Any())
        {
            _output.WriteLine("\nFailed Migrations:");
            foreach (var (file, error) in result.FailedMigrations)
            {
                _output.WriteLine($"  ✗ {file}: {error}");
            }
        }
        
        // Generate project file update
        var projectUpdate = _migrationHelper.GenerateProjectFileUpdate();
        _output.WriteLine("\n--- Project File Update Required ---");
        _output.WriteLine(projectUpdate);
        
        Assert.True(result.SuccessfulMigrations.Count > 0, "At least some samples should be migrated");
    }

    [Fact]
    public Task VerifyEmbeddedResourcesAvailable()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<EmbeddedResourceProvider>>();
        var provider = new EmbeddedResourceProvider(logger);
        
        var availableResources = provider.GetAvailableEmbeddedLogs().ToList();
        
        _output.WriteLine($"Found {availableResources.Count} embedded resources:");
        foreach (var resource in availableResources)
        {
            _output.WriteLine($"  - {resource}");
        }
        
        // Verify critical samples are available
        var criticalSamples = CriticalSampleLogs.GetAllCriticalSamples().ToList();
        var missingCritical = criticalSamples.Except(availableResources).ToList();
        
        if (missingCritical.Any())
        {
            _output.WriteLine("\nMissing Critical Resources:");
            foreach (var missing in missingCritical)
            {
                _output.WriteLine($"  ! {missing}");
            }
        }
        
        Assert.Empty(missingCritical);
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnalyzeTestFilesForMigration()
    {
        var testFiles = new[]
        {
            @"C:\Users\evild\RiderProjects\Scanner111\Scanner111.Test\Integration\SampleLogAnalysisIntegrationTests.cs",
            @"C:\Users\evild\RiderProjects\Scanner111\Scanner111.Test\Integration\SampleOutputValidationTests.cs",
            @"C:\Users\evild\RiderProjects\Scanner111\Scanner111.Test\Analysis\Analyzers\SettingsAnalyzerSampleDataTests.cs"
        };
        
        foreach (var testFile in testFiles)
        {
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                continue;
            }
            
            var analysis = await _migrationHelper.AnalyzeTestFileAsync(testFile);
            
            _output.WriteLine($"\n--- Analysis: {Path.GetFileName(testFile)} ---");
            _output.WriteLine($"  Uses SampleDataBase: {analysis.UsesSampleDataBase}");
            _output.WriteLine($"  Sample Log References: {analysis.SampleLogReferences}");
            _output.WriteLine($"  Direct Sample References: {analysis.DirectSampleReferences}");
            _output.WriteLine($"  Uses Output Validation: {analysis.UsesOutputValidation}");
            _output.WriteLine($"  Complexity Score: {analysis.ComplexityScore}");
            
            if (analysis.TestMethodsToMigrate.Any())
            {
                _output.WriteLine($"  Methods to Migrate:");
                foreach (var method in analysis.TestMethodsToMigrate)
                {
                    _output.WriteLine($"    - {method}");
                }
            }
            
            if (analysis.Recommendations.Any())
            {
                _output.WriteLine($"  Recommendations:");
                foreach (var rec in analysis.Recommendations)
                {
                    _output.WriteLine($"    • {rec}");
                }
            }
        }
    }
}

/// <summary>
///     Extension method to add xUnit test output to logging.
/// </summary>
public static class LoggingExtensions
{
    public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.AddProvider(new XUnitLoggerProvider(output));
        return builder;
    }
}

/// <summary>
///     Logger provider for xUnit test output.
/// </summary>
public class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XUnitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLogger(_output, categoryName);
    }

    public void Dispose()
    {
    }
}

/// <summary>
///     Logger implementation for xUnit test output.
/// </summary>
public class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XUnitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine(exception.ToString());
        }
    }
}