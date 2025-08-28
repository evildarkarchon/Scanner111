using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Test.Infrastructure.TestData;

namespace Scanner111.Test.Infrastructure;

/// <summary>
///     Base class for tests that need to work with sample log files and expected outputs.
///     Provides utilities for accessing sample data while maintaining test isolation.
/// </summary>
public abstract class SampleDataTestBase : IntegrationTestBase
{
    private readonly string _sampleLogsRoot;
    private readonly string _sampleOutputRoot;
    private readonly EmbeddedResourceProvider _embeddedProvider;
    private readonly bool _useEmbeddedResourcesOnly;
    
    protected SampleDataTestBase()
    {
        // Find sample directories relative to project root
        var projectRoot = FindProjectRoot();
        _sampleLogsRoot = Path.Combine(projectRoot, "sample_logs");
        _sampleOutputRoot = Path.Combine(projectRoot, "sample_output");
        
        // Initialize embedded resource provider
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _embeddedProvider = new EmbeddedResourceProvider(loggerFactory.CreateLogger<EmbeddedResourceProvider>());
        
        // Check if we should use embedded resources only (for CI/CD environments)
        _useEmbeddedResourcesOnly = Environment.GetEnvironmentVariable("USE_EMBEDDED_RESOURCES_ONLY") == "true" ||
                                   (!Directory.Exists(_sampleLogsRoot) && !Directory.Exists(_sampleOutputRoot));
        
        if (!_useEmbeddedResourcesOnly)
        {
            // Only validate directories if not using embedded resources only
            if (!Directory.Exists(_sampleLogsRoot))
                Logger.LogWarning("Sample logs directory not found: {Path}. Will use embedded resources.", _sampleLogsRoot);
            if (!Directory.Exists(_sampleOutputRoot))
                Logger.LogWarning("Sample output directory not found: {Path}. Will use embedded resources.", _sampleOutputRoot);
        }
    }

    /// <summary>
    ///     Gets all available FO4 sample log files.
    /// </summary>
    protected IEnumerable<string> GetFo4SampleLogs()
    {
        // Try embedded resources first
        var embeddedLogs = _embeddedProvider.GetAvailableEmbeddedLogs()
            .Where(name => name.EndsWith(".log"))
            .ToList();
        
        if (embeddedLogs.Any())
        {
            return embeddedLogs;
        }
        
        // Fall back to file system if no embedded resources and not in embedded-only mode
        if (!_useEmbeddedResourcesOnly)
        {
            var fo4Dir = Path.Combine(_sampleLogsRoot, "FO4");
            if (Directory.Exists(fo4Dir))
            {
                return Directory.GetFiles(fo4Dir, "*.log")
                    .Select(Path.GetFileName)
                    .OrderBy(f => f);
            }
        }
        
        return Enumerable.Empty<string>();
    }

    /// <summary>
    ///     Gets all expected output files.
    /// </summary>
    protected IEnumerable<string> GetExpectedOutputs()
    {
        return Directory.GetFiles(_sampleOutputRoot, "*.md")
            .OrderBy(f => f);
    }

    /// <summary>
    ///     Copies a sample log file to the test directory for isolated testing.
    /// </summary>
    protected async Task<string> CopySampleLogToTestDirAsync(string sampleLogPath)
    {
        if (!File.Exists(sampleLogPath))
            throw new FileNotFoundException($"Sample log not found: {sampleLogPath}");
        
        var fileName = Path.GetFileName(sampleLogPath);
        var destPath = Path.Combine(TestDirectory, fileName);
        
        await Task.Run(() => File.Copy(sampleLogPath, destPath, overwrite: true))
            .ConfigureAwait(false);
        
        return destPath;
    }

    /// <summary>
    ///     Reads a sample log file content.
    /// </summary>
    protected async Task<string> ReadSampleLogAsync(string logFileName)
    {
        // Try embedded resources first
        try
        {
            return await _embeddedProvider.GetEmbeddedLogAsync(logFileName, TestCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (!_useEmbeddedResourcesOnly)
        {
            // Fall back to file system if embedded resource not found
            var fo4Dir = Path.Combine(_sampleLogsRoot, "FO4");
            var logPath = Path.Combine(fo4Dir, logFileName);
            
            if (!File.Exists(logPath))
                throw new FileNotFoundException($"Sample log not found in embedded resources or file system: {logFileName}");
            
            Logger.LogDebug("Reading sample log from file system: {Path}", logPath);
            return await File.ReadAllTextAsync(logPath).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reads expected output content for a given crash log.
    /// </summary>
    protected async Task<string?> ReadExpectedOutputAsync(string crashLogName)
    {
        // Try embedded resources first
        var expectedOutput = await _embeddedProvider.GetEmbeddedExpectedOutputAsync(crashLogName, TestCancellation.Token)
            .ConfigureAwait(false);
        
        if (expectedOutput != null)
        {
            return expectedOutput;
        }
        
        // Fall back to file system if not in embedded-only mode
        if (!_useEmbeddedResourcesOnly)
        {
            // Convert log name to expected output name
            // e.g., "crash-2023-09-15-01-54-49.log" -> "crash-2023-09-15-01-54-49-AUTOSCAN.md"
            var baseName = Path.GetFileNameWithoutExtension(crashLogName);
            var outputFileName = $"{baseName}-AUTOSCAN.md";
            var outputPath = Path.Combine(_sampleOutputRoot, outputFileName);
            
            if (File.Exists(outputPath))
            {
                Logger.LogDebug("Reading expected output from file system: {Path}", outputPath);
                return await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
            }
        }
        
        return null;
    }

    /// <summary>
    ///     Gets a random sample log file path for testing.
    /// </summary>
    protected string GetRandomSampleLog()
    {
        var logs = GetFo4SampleLogs().ToList();
        if (!logs.Any())
            throw new InvalidOperationException("No sample logs found in embedded resources or file system");
        
        var random = new Random();
        return logs[random.Next(logs.Count)];
    }

    /// <summary>
    ///     Creates an embedded resource copy of a sample file for permanent test fixtures.
    ///     This helps prepare for when sample directories are removed.
    /// </summary>
    protected async Task<string> CreateEmbeddedResourceCopyAsync(
        string sampleFilePath,
        string resourceName,
        [CallerMemberName] string testMethod = "")
    {
        if (!File.Exists(sampleFilePath))
            throw new FileNotFoundException($"Sample file not found: {sampleFilePath}");
        
        var content = await File.ReadAllTextAsync(sampleFilePath).ConfigureAwait(false);
        var embeddedDir = Path.Combine(TestDirectory, "EmbeddedResources", testMethod);
        Directory.CreateDirectory(embeddedDir);
        
        var destPath = Path.Combine(embeddedDir, resourceName);
        await File.WriteAllTextAsync(destPath, content).ConfigureAwait(false);
        
        Logger.LogInformation(
            "Created embedded resource copy: {ResourceName} from {SourceFile}",
            resourceName, Path.GetFileName(sampleFilePath));
        
        return destPath;
    }

    /// <summary>
    ///     Gets matching sample log and expected output pairs for validation testing.
    /// </summary>
    protected IEnumerable<(string LogPath, string OutputPath)> GetMatchingSamplePairs()
    {
        var outputs = GetExpectedOutputs().ToList();
        
        foreach (var outputPath in outputs)
        {
            var outputName = Path.GetFileNameWithoutExtension(outputPath);
            // Remove "-AUTOSCAN" suffix to get log name
            var logName = outputName.Replace("-AUTOSCAN", "") + ".log";
            var logPath = Path.Combine(_sampleLogsRoot, "FO4", logName);
            
            if (File.Exists(logPath))
            {
                yield return (logPath, outputPath);
            }
        }
    }

    /// <summary>
    ///     Validates that a test output matches expected patterns from sample output.
    /// </summary>
    protected void ValidateAgainstExpectedOutput(string actualOutput, string expectedOutput)
    {
        // Extract key sections from expected output for validation
        var expectedLines = expectedOutput.Split('\n', StringSplitOptions.TrimEntries);
        var actualLines = actualOutput.Split('\n', StringSplitOptions.TrimEntries);
        
        // Check for main error detection
        var expectedError = expectedLines.FirstOrDefault(l => l.StartsWith("**Main Error:**"));
        if (!string.IsNullOrEmpty(expectedError))
        {
            var errorPattern = ExtractErrorPattern(expectedError);
            actualOutput.Should().Contain(errorPattern,
                $"Expected to find error pattern: {errorPattern}");
        }
        
        // Check for version detection
        var expectedVersion = expectedLines.FirstOrDefault(l => l.Contains("Buffout 4 v"));
        if (!string.IsNullOrEmpty(expectedVersion))
        {
            actualOutput.Should().MatchEquivalentOf("*Buffout 4 v*",
                "Expected to detect Buffout 4 version");
        }
        
        // Check for suspect detection patterns
        if (expectedOutput.Contains("SUSPECT FOUND"))
        {
            actualOutput.Should().MatchEquivalentOf("*SUSPECT*",
                "Expected to detect suspects when present in sample");
        }
    }

    private static string ExtractErrorPattern(string errorLine)
    {
        // Extract the core error type from the line
        if (errorLine.Contains("EXCEPTION_ACCESS_VIOLATION"))
            return "EXCEPTION_ACCESS_VIOLATION";
        if (errorLine.Contains("EXCEPTION_STACK_OVERFLOW"))
            return "EXCEPTION_STACK_OVERFLOW";
        // Add more patterns as needed
        
        return errorLine.Replace("**Main Error:**", "").Trim();
    }

    private static string FindProjectRoot([CallerFilePath] string sourceFilePath = "")
    {
        var directory = new FileInfo(sourceFilePath).Directory;
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Scanner111.sln")))
        {
            directory = directory.Parent;
        }
        
        if (directory == null)
            throw new InvalidOperationException("Could not find project root directory");
        
        return directory.FullName;
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Add logging specifically for sample data operations
        services.AddLogging(builder =>
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Debug)
                   .AddFilter("Scanner111.Test.Infrastructure", LogLevel.Trace);
        });
    }
}

/// <summary>
///     Theory data for sample-based tests.
/// </summary>
public class SampleLogTheoryData : TheoryData<string>
{
    public SampleLogTheoryData()
    {
        // First try to get embedded resources
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var embeddedProvider = new EmbeddedResourceProvider(loggerFactory.CreateLogger<EmbeddedResourceProvider>());
        var embeddedLogs = embeddedProvider.GetAvailableEmbeddedLogs()
            .Where(name => name.EndsWith(".log"))
            .ToList();
        
        if (embeddedLogs.Any())
        {
            // Use all available embedded logs for theory testing
            foreach (var log in embeddedLogs.Take(5)) // Limit to 5 for speed
            {
                Add(log);
            }
        }
        else
        {
            // Fall back to file system
            var projectRoot = FindProjectRoot();
            var fo4Dir = Path.Combine(projectRoot, "sample_logs", "FO4");
            
            if (Directory.Exists(fo4Dir))
            {
                // Add a subset of diverse samples for theory testing
                var samples = new[]
                {
                    "crash-2022-06-05-12-52-17.log", // Early sample
                    "crash-2022-06-09-07-25-03.log", // Stack overflow
                    "crash-2022-06-12-07-11-38.log", // Large log
                    "crash-2022-06-15-10-02-51.log", // Minimal log
                };
                
                foreach (var sample in samples)
                {
                    var path = Path.Combine(fo4Dir, sample);
                    if (File.Exists(path))
                        Add(sample);
                }
            }
        }
    }
    
    private static string FindProjectRoot([CallerFilePath] string sourceFilePath = "")
    {
        var directory = new FileInfo(sourceFilePath).Directory;
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Scanner111.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root");
    }
}