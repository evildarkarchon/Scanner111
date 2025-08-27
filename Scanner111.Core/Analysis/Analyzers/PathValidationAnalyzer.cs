using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Validates all configured paths for accessibility and correctness.
/// </summary>
public sealed class PathValidationAnalyzer : AnalyzerBase
{
    private readonly IYamlSettingsCache _settingsCache;
    
    public PathValidationAnalyzer(
        ILogger<PathValidationAnalyzer> logger,
        IYamlSettingsCache settingsCache) : base(logger)
    {
        _settingsCache = settingsCache ?? throw new ArgumentNullException(nameof(settingsCache));
    }
    
    public override string Name => "PathValidation";
    
    public override string DisplayName => "Path Validation";
    
    public override int Priority => 5; // Run very early to ensure paths are valid
    
    public override TimeSpan Timeout => TimeSpan.FromSeconds(10);
    
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting path validation analysis");
        
        var validationResults = new List<PathValidationResult>();
        
        // Get all paths to validate from context or configuration
        var pathsToValidate = await GetPathsToValidateAsync(context).ConfigureAwait(false);
        
        // Validate each path
        foreach (var path in pathsToValidate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var validationResult = await ValidatePathAsync(path, cancellationToken).ConfigureAwait(false);
            validationResults.Add(validationResult);
        }
        
        // Build report
        var report = BuildValidationReport(validationResults);
        
        // Determine severity
        var hasErrors = validationResults.Any(r => r.Severity == PathSeverity.Error);
        var hasWarnings = validationResults.Any(r => r.Severity == PathSeverity.Warning);
        
        // Create fragment
        ReportFragment fragment;
        AnalysisSeverity severity;
        
        if (hasErrors)
        {
            fragment = ReportFragment.CreateError("Path Validation Errors", report, 15);
            severity = AnalysisSeverity.Error;
        }
        else if (hasWarnings)
        {
            fragment = ReportFragment.CreateWarning("Path Validation Warnings", report, 25);
            severity = AnalysisSeverity.Warning;
        }
        else
        {
            fragment = ReportFragment.CreateSection("Path Validation", report, 90);
            severity = AnalysisSeverity.None;
        }
        
        var result = new AnalysisResult(Name)
        {
            Success = true,
            Fragment = fragment,
            Severity = severity
        };
        
        // Add metadata
        result.AddMetadata("TotalPaths", pathsToValidate.Count);
        result.AddMetadata("ValidPaths", validationResults.Count(r => r.IsValid));
        result.AddMetadata("InvalidPaths", validationResults.Count(r => !r.IsValid));
        
        // Store valid paths in context for other analyzers
        foreach (var validPath in validationResults.Where(r => r.IsValid))
        {
            context.SetSharedData($"ValidPath.{validPath.Name}", validPath.Path);
        }
        
        return result;
    }
    
    private async Task<List<PathInfo>> GetPathsToValidateAsync(AnalysisContext context)
    {
        var paths = new List<PathInfo>();
        
        // Add standard paths to check
        paths.Add(new PathInfo
        {
            Name = "GameExecutable",
            Path = @"C:\Games\Fallout4\Fallout4.exe", // Would come from settings
            Type = PathType.File,
            IsRequired = true
        });
        
        paths.Add(new PathInfo
        {
            Name = "GameData",
            Path = @"C:\Games\Fallout4\Data", // Would come from settings
            Type = PathType.Directory,
            IsRequired = true
        });
        
        paths.Add(new PathInfo
        {
            Name = "Documents",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Type = PathType.Directory,
            IsRequired = true
        });
        
        paths.Add(new PathInfo
        {
            Name = "ModOrganizer",
            Path = @"C:\Modding\MO2\ModOrganizer.exe", // Would come from settings
            Type = PathType.File,
            IsRequired = false
        });
        
        // Add input path if it's a file or directory
        if (!string.IsNullOrEmpty(context.InputPath))
        {
            paths.Add(new PathInfo
            {
                Name = "InputPath",
                Path = context.InputPath,
                Type = File.Exists(context.InputPath) ? PathType.File : PathType.Directory,
                IsRequired = true
            });
        }
        
        await Task.CompletedTask.ConfigureAwait(false);
        return paths;
    }
    
    private async Task<PathValidationResult> ValidatePathAsync(
        PathInfo pathInfo,
        CancellationToken cancellationToken)
    {
        var result = new PathValidationResult
        {
            Name = pathInfo.Name,
            Path = pathInfo.Path,
            IsValid = false,
            Severity = PathSeverity.None
        };
        
        try
        {
            // Check if path exists
            bool exists = pathInfo.Type == PathType.File 
                ? File.Exists(pathInfo.Path)
                : Directory.Exists(pathInfo.Path);
            
            if (!exists)
            {
                result.IsValid = false;
                result.Message = pathInfo.IsRequired 
                    ? $"Required {pathInfo.Type.ToString().ToLower()} not found"
                    : $"Optional {pathInfo.Type.ToString().ToLower()} not found";
                result.Severity = pathInfo.IsRequired ? PathSeverity.Error : PathSeverity.Info;
                return result;
            }
            
            // Check accessibility
            if (pathInfo.Type == PathType.File)
            {
                try
                {
                    using var stream = File.Open(pathInfo.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    result.IsValid = true;
                    result.Message = "File is accessible";
                }
                catch (UnauthorizedAccessException)
                {
                    result.IsValid = false;
                    result.Message = "File exists but cannot be accessed (permission denied)";
                    result.Severity = PathSeverity.Warning;
                }
            }
            else
            {
                try
                {
                    var files = Directory.GetFiles(pathInfo.Path, "*", SearchOption.TopDirectoryOnly);
                    result.IsValid = true;
                    result.Message = $"Directory is accessible ({files.Length} files)";
                }
                catch (UnauthorizedAccessException)
                {
                    result.IsValid = false;
                    result.Message = "Directory exists but cannot be accessed (permission denied)";
                    result.Severity = PathSeverity.Warning;
                }
            }
            
            // Check for problematic path characteristics
            if (result.IsValid)
            {
                if (pathInfo.Path.Length > 200)
                {
                    result.Message += " [Path may be too long for some operations]";
                    result.Severity = PathSeverity.Warning;
                }
                
                if (pathInfo.Path.Contains("Program Files") && pathInfo.Type == PathType.Directory)
                {
                    result.Message += " [Located in Program Files - may have permission issues]";
                    result.Severity = PathSeverity.Warning;
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Error validating path: {ex.Message}";
            result.Severity = PathSeverity.Warning;
        }
        
        await Task.CompletedTask.ConfigureAwait(false);
        return result;
    }
    
    private string BuildValidationReport(List<PathValidationResult> results)
    {
        var sb = new StringBuilder();
        
        // Group by validity
        var validPaths = results.Where(r => r.IsValid).ToList();
        var invalidPaths = results.Where(r => !r.IsValid).ToList();
        
        sb.AppendLine($"**Path Validation Summary**");
        sb.AppendLine($"- Total paths checked: {results.Count}");
        sb.AppendLine($"- Valid paths: {validPaths.Count}");
        sb.AppendLine($"- Invalid paths: {invalidPaths.Count}");
        sb.AppendLine();
        
        // Show errors first
        var errors = results.Where(r => r.Severity == PathSeverity.Error).ToList();
        if (errors.Any())
        {
            sb.AppendLine("**Errors:**");
            foreach (var error in errors)
            {
                sb.AppendLine($"- ❌ **{error.Name}**: {error.Message}");
                sb.AppendLine($"  Path: `{error.Path}`");
            }
            sb.AppendLine();
        }
        
        // Show warnings
        var warnings = results.Where(r => r.Severity == PathSeverity.Warning).ToList();
        if (warnings.Any())
        {
            sb.AppendLine("**Warnings:**");
            foreach (var warning in warnings)
            {
                sb.AppendLine($"- ⚠️ **{warning.Name}**: {warning.Message}");
                sb.AppendLine($"  Path: `{warning.Path}`");
            }
            sb.AppendLine();
        }
        
        // Show valid paths in verbose mode
        if (validPaths.Any())
        {
            sb.AppendLine("**Valid Paths:**");
            foreach (var valid in validPaths.Take(5))
            {
                sb.AppendLine($"- ✅ **{valid.Name}**: {valid.Message}");
            }
            
            if (validPaths.Count > 5)
            {
                sb.AppendLine($"- ... and {validPaths.Count - 5} more valid paths");
            }
        }
        
        return sb.ToString();
    }
    
    private class PathInfo
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required PathType Type { get; init; }
        public required bool IsRequired { get; init; }
    }
    
    private class PathValidationResult
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public PathSeverity Severity { get; set; }
    }
    
    private enum PathType
    {
        File,
        Directory
    }
    
    private enum PathSeverity
    {
        None,
        Info,
        Warning,
        Error
    }
}