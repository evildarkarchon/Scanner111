using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Test.Infrastructure.Migration;

/// <summary>
///     Helps migrate existing tests from sample_logs dependency to embedded/synthetic data.
///     Provides utilities for converting sample-based tests to self-contained tests.
/// </summary>
public class TestMigrationHelper
{
    private readonly ILogger<TestMigrationHelper> _logger;
    private readonly string _sampleLogsRoot;
    private readonly string _sampleOutputRoot;
    private readonly string _embeddedResourcesPath;

    public TestMigrationHelper(ILogger<TestMigrationHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var projectRoot = FindProjectRoot();
        _sampleLogsRoot = Path.Combine(projectRoot, "sample_logs");
        _sampleOutputRoot = Path.Combine(projectRoot, "sample_output");
        _embeddedResourcesPath = Path.Combine(projectRoot, "Scanner111.Test", "Resources", "EmbeddedLogs");
    }

    /// <summary>
    ///     Migrates a sample log file to an embedded resource.
    /// </summary>
    public async Task<MigrationResult> MigrateSampleToEmbeddedAsync(
        string sampleLogPath, 
        CancellationToken cancellationToken = default)
    {
        var result = new MigrationResult
        {
            SourcePath = sampleLogPath,
            Success = false
        };

        try
        {
            if (!File.Exists(sampleLogPath))
            {
                result.ErrorMessage = $"Sample file not found: {sampleLogPath}";
                return result;
            }

            var fileName = Path.GetFileName(sampleLogPath);
            result.ResourceName = fileName;
            
            // Create embedded resources directory if needed
            Directory.CreateDirectory(_embeddedResourcesPath);
            
            var destPath = Path.Combine(_embeddedResourcesPath, fileName);
            result.EmbeddedPath = destPath;

            // Copy the file
            await Task.Run(() => File.Copy(sampleLogPath, destPath, overwrite: true), cancellationToken)
                .ConfigureAwait(false);

            // Check for corresponding output file
            var outputFileName = Path.GetFileNameWithoutExtension(fileName) + "-AUTOSCAN.md";
            var outputPath = Path.Combine(_sampleOutputRoot, outputFileName);
            
            if (File.Exists(outputPath))
            {
                var outputDestPath = Path.Combine(_embeddedResourcesPath, outputFileName);
                await Task.Run(() => File.Copy(outputPath, outputDestPath, overwrite: true), cancellationToken)
                    .ConfigureAwait(false);
                result.HasExpectedOutput = true;
            }

            result.Success = true;
            _logger.LogInformation("Successfully migrated {FileName} to embedded resource", fileName);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to migrate sample to embedded resource");
        }

        return result;
    }

    /// <summary>
    ///     Generates migration code for converting a test method.
    /// </summary>
    public string GenerateMigrationCode(string originalTestCode, MigrationOptions options)
    {
        var sb = new StringBuilder();
        var lines = originalTestCode.Split('\n');
        
        foreach (var line in lines)
        {
            var migratedLine = MigrateLine(line, options);
            sb.AppendLine(migratedLine);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Analyzes a test file and suggests migration strategies.
    /// </summary>
    public async Task<MigrationAnalysis> AnalyzeTestFileAsync(
        string testFilePath,
        CancellationToken cancellationToken = default)
    {
        var analysis = new MigrationAnalysis
        {
            TestFilePath = testFilePath
        };

        if (!File.Exists(testFilePath))
        {
            analysis.Issues.Add($"Test file not found: {testFilePath}");
            return analysis;
        }

        var content = await File.ReadAllTextAsync(testFilePath, cancellationToken).ConfigureAwait(false);
        
        // Check for sample data dependencies
        if (content.Contains("SampleDataTestBase"))
        {
            analysis.UsesSampleDataBase = true;
            analysis.Recommendations.Add("Inherit from SnapshotTestBase or EmbeddedResourceTestBase instead");
        }

        if (content.Contains("ReadSampleLogAsync"))
        {
            analysis.SampleLogReferences++;
            analysis.Recommendations.Add("Replace ReadSampleLogAsync with EmbeddedResourceProvider.GetEmbeddedLogAsync");
        }

        if (content.Contains("sample_logs"))
        {
            analysis.DirectSampleReferences++;
            analysis.Recommendations.Add("Remove direct references to sample_logs directory");
        }

        if (content.Contains("GetFo4SampleLogs"))
        {
            analysis.SampleLogReferences++;
            analysis.Recommendations.Add("Replace with EmbeddedResourceProvider.GetAvailableEmbeddedLogs");
        }

        if (content.Contains("ValidateAgainstExpectedOutput"))
        {
            analysis.UsesOutputValidation = true;
            analysis.Recommendations.Add("Consider using Verify.Xunit snapshot testing instead");
        }

        // Identify test methods that need migration
        var testMethodPattern = @"\[(?:Fact|Theory)\][\s\S]*?public\s+async\s+Task\s+(\w+)\s*\(";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, testMethodPattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var methodName = match.Groups[1].Value;
            var methodContent = ExtractMethodContent(content, match.Index);
            
            if (methodContent.Contains("ReadSampleLogAsync") || 
                methodContent.Contains("sample_logs"))
            {
                analysis.TestMethodsToMigrate.Add(methodName);
            }
        }

        // Calculate migration complexity
        analysis.ComplexityScore = CalculateComplexityScore(analysis);
        
        return analysis;
    }

    /// <summary>
    ///     Batch migrates critical sample files to embedded resources.
    /// </summary>
    public async Task<BatchMigrationResult> MigrateCriticalSamplesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new BatchMigrationResult();
        
        var criticalSamples = new[]
        {
            "crash-2022-06-05-12-52-17.log",
            "crash-2023-09-15-01-54-49.log",
            "crash-2023-11-08-05-46-35.log",
            "crash-2024-08-25-11-05-43.log",
            "crash-2022-06-09-07-25-03.log",
            "crash-2023-10-14-05-54-22.log",
            "crash-2023-10-25-09-49-04.log",
            "crash-2023-12-01-08-33-44.log",
            "crash-2022-06-12-07-11-38.log",
            "crash-2022-06-15-10-02-51.log"
        };

        foreach (var sample in criticalSamples)
        {
            var samplePath = Path.Combine(_sampleLogsRoot, "FO4", sample);
            var migrationResult = await MigrateSampleToEmbeddedAsync(samplePath, cancellationToken)
                .ConfigureAwait(false);
            
            if (migrationResult.Success)
            {
                result.SuccessfulMigrations.Add(sample);
            }
            else
            {
                result.FailedMigrations.Add((sample, migrationResult.ErrorMessage ?? "Unknown error"));
            }
        }

        result.TotalAttempted = criticalSamples.Length;
        _logger.LogInformation(
            "Batch migration completed: {Success}/{Total} files migrated successfully",
            result.SuccessfulMigrations.Count, result.TotalAttempted);

        return result;
    }

    /// <summary>
    ///     Creates a project file update for embedded resources.
    /// </summary>
    public string GenerateProjectFileUpdate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Add to Scanner111.Test.csproj ItemGroup -->");
        sb.AppendLine("<ItemGroup>");
        
        if (Directory.Exists(_embeddedResourcesPath))
        {
            var files = Directory.GetFiles(_embeddedResourcesPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(
                    Path.GetDirectoryName(_embeddedResourcesPath)!, 
                    file).Replace('\\', '/');
                sb.AppendLine($"  <EmbeddedResource Include=\"Resources/EmbeddedLogs/{Path.GetFileName(file)}\" />");
            }
        }
        
        sb.AppendLine("</ItemGroup>");
        return sb.ToString();
    }

    private string MigrateLine(string line, MigrationOptions options)
    {
        var migrated = line;

        if (options.ReplaceBaseClass)
        {
            migrated = migrated.Replace(": SampleDataTestBase", ": " + options.NewBaseClass);
        }

        if (options.ReplaceSampleReads)
        {
            migrated = migrated.Replace("ReadSampleLogAsync", "GetEmbeddedLogAsync");
            migrated = migrated.Replace("GetFo4SampleLogs()", "GetAvailableEmbeddedLogs()");
        }

        if (options.UseSnapshotTesting)
        {
            migrated = migrated.Replace("ValidateAgainstExpectedOutput", "VerifyAsync");
        }

        if (options.UseSyntheticData && line.Contains("sample_logs"))
        {
            migrated = "// TODO: Replace with synthetic data generator\n// " + migrated;
        }

        return migrated;
    }

    private string ExtractMethodContent(string fileContent, int methodStartIndex)
    {
        var braceCount = 0;
        var inMethod = false;
        var methodContent = new StringBuilder();
        
        for (int i = methodStartIndex; i < fileContent.Length; i++)
        {
            var ch = fileContent[i];
            methodContent.Append(ch);
            
            if (ch == '{')
            {
                braceCount++;
                inMethod = true;
            }
            else if (ch == '}' && inMethod)
            {
                braceCount--;
                if (braceCount == 0)
                {
                    break;
                }
            }
        }
        
        return methodContent.ToString();
    }

    private int CalculateComplexityScore(MigrationAnalysis analysis)
    {
        var score = 0;
        
        score += analysis.SampleLogReferences * 2;
        score += analysis.DirectSampleReferences * 3;
        score += analysis.TestMethodsToMigrate.Count * 5;
        
        if (analysis.UsesSampleDataBase) score += 10;
        if (analysis.UsesOutputValidation) score += 5;
        
        return score;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Scanner111.sln")))
        {
            directory = directory.Parent;
        }
        
        if (directory == null)
            throw new InvalidOperationException("Could not find project root directory");
        
        return directory.FullName;
    }
}

/// <summary>
///     Result of migrating a sample file to embedded resource.
/// </summary>
public class MigrationResult
{
    public string SourcePath { get; set; } = "";
    public string ResourceName { get; set; } = "";
    public string EmbeddedPath { get; set; } = "";
    public bool HasExpectedOutput { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
///     Analysis of a test file for migration.
/// </summary>
public class MigrationAnalysis
{
    public string TestFilePath { get; set; } = "";
    public bool UsesSampleDataBase { get; set; }
    public bool UsesOutputValidation { get; set; }
    public int SampleLogReferences { get; set; }
    public int DirectSampleReferences { get; set; }
    public List<string> TestMethodsToMigrate { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public int ComplexityScore { get; set; }
}

/// <summary>
///     Options for code migration.
/// </summary>
public class MigrationOptions
{
    public bool ReplaceBaseClass { get; set; } = true;
    public string NewBaseClass { get; set; } = "EmbeddedResourceTestBase";
    public bool ReplaceSampleReads { get; set; } = true;
    public bool UseSnapshotTesting { get; set; } = true;
    public bool UseSyntheticData { get; set; } = false;
}

/// <summary>
///     Result of batch migration operation.
/// </summary>
public class BatchMigrationResult
{
    public int TotalAttempted { get; set; }
    public List<string> SuccessfulMigrations { get; set; } = new();
    public List<(string FileName, string Error)> FailedMigrations { get; set; } = new();
    
    public bool AllSuccessful => FailedMigrations.Count == 0;
    public double SuccessRate => TotalAttempted > 0 
        ? (double)SuccessfulMigrations.Count / TotalAttempted 
        : 0;
}