using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Scanner111.Test.Infrastructure;

/// <summary>
///     Base class for tests that need to work with sample log files and expected outputs.
///     Provides utilities for accessing sample data while maintaining test isolation.
/// </summary>
public abstract class SampleDataTestBase : IntegrationTestBase
{
    private readonly string _sampleLogsRoot;
    private readonly string _sampleOutputRoot;
    
    protected SampleDataTestBase()
    {
        // Find sample directories relative to project root
        var projectRoot = FindProjectRoot();
        _sampleLogsRoot = Path.Combine(projectRoot, "sample_logs");
        _sampleOutputRoot = Path.Combine(projectRoot, "sample_output");
        
        if (!Directory.Exists(_sampleLogsRoot))
            throw new DirectoryNotFoundException($"Sample logs directory not found: {_sampleLogsRoot}");
        if (!Directory.Exists(_sampleOutputRoot))
            throw new DirectoryNotFoundException($"Sample output directory not found: {_sampleOutputRoot}");
    }

    /// <summary>
    ///     Gets all available FO4 sample log files.
    /// </summary>
    protected IEnumerable<string> GetFo4SampleLogs()
    {
        var fo4Dir = Path.Combine(_sampleLogsRoot, "FO4");
        if (!Directory.Exists(fo4Dir))
            return Enumerable.Empty<string>();
        
        return Directory.GetFiles(fo4Dir, "*.log")
            .OrderBy(f => f);
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
        var fo4Dir = Path.Combine(_sampleLogsRoot, "FO4");
        var logPath = Path.Combine(fo4Dir, logFileName);
        
        if (!File.Exists(logPath))
            throw new FileNotFoundException($"Sample log not found: {logPath}");
        
        return await File.ReadAllTextAsync(logPath).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads expected output content for a given crash log.
    /// </summary>
    protected async Task<string?> ReadExpectedOutputAsync(string crashLogName)
    {
        // Convert log name to expected output name
        // e.g., "crash-2023-09-15-01-54-49.log" -> "crash-2023-09-15-01-54-49-AUTOSCAN.md"
        var baseName = Path.GetFileNameWithoutExtension(crashLogName);
        var outputFileName = $"{baseName}-AUTOSCAN.md";
        var outputPath = Path.Combine(_sampleOutputRoot, outputFileName);
        
        if (!File.Exists(outputPath))
            return null;
        
        return await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a random sample log file path for testing.
    /// </summary>
    protected string GetRandomSampleLog()
    {
        var logs = GetFo4SampleLogs().ToList();
        if (!logs.Any())
            throw new InvalidOperationException("No sample logs found");
        
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
        var projectRoot = FindProjectRoot();
        var fo4Dir = Path.Combine(projectRoot, "sample_logs", "FO4");
        
        if (Directory.Exists(fo4Dir))
        {
            // Add a subset of diverse samples for theory testing
            var samples = new[]
            {
                "crash-2022-06-05-12-52-17.log", // Early sample
                "crash-2023-09-15-01-54-49.log", // Has matching output
                "crash-2023-11-08-05-46-35.log", // Has matching output
                "crash-2024-08-25-11-05-43.log", // Recent sample
            };
            
            foreach (var sample in samples)
            {
                var path = Path.Combine(fo4Dir, sample);
                if (File.Exists(path))
                    Add(sample);
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