using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes the documents folder configuration for potential issues.
/// </summary>
public sealed class DocumentsPathAnalyzer : AnalyzerBase
{
    private readonly IAsyncYamlSettingsCore _yamlCore;
    
    public DocumentsPathAnalyzer(
        ILogger<DocumentsPathAnalyzer> logger,
        IAsyncYamlSettingsCore yamlCore) : base(logger)
    {
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }
    
    public override string Name => "DocumentsPath";
    
    public override string DisplayName => "Documents Path Configuration";
    
    public override int Priority => 20; // Run after game integrity
    
    public override TimeSpan Timeout => TimeSpan.FromSeconds(5);
    
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting documents path analysis");
        
        var report = new StringBuilder();
        var hasWarnings = false;
        var hasErrors = false;
        
        // Check OneDrive configuration
        var oneDriveCheck = await CheckOneDriveConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (!oneDriveCheck.isValid)
        {
            report.AppendLine(oneDriveCheck.message);
            hasWarnings = true;
        }
        
        // Check INI file configuration
        var iniCheck = await CheckIniFilesAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(iniCheck.message))
        {
            report.AppendLine(iniCheck.message);
            if (!iniCheck.isValid)
            {
                hasErrors = true;
            }
        }
        
        // Check folder permissions
        var permissionCheck = await CheckFolderPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (!permissionCheck.isValid)
        {
            report.AppendLine(permissionCheck.message);
            hasWarnings = true;
        }
        
        // If no issues found, add success message
        if (!hasWarnings && !hasErrors)
        {
            report.AppendLine("✅ Documents folder configuration is optimal");
        }
        
        // Create appropriate fragment
        ReportFragment fragment;
        AnalysisSeverity severity;
        
        if (hasErrors)
        {
            fragment = ReportFragment.CreateError("Documents Configuration Issues", report.ToString(), 25);
            severity = AnalysisSeverity.Error;
        }
        else if (hasWarnings)
        {
            fragment = ReportFragment.CreateWarning("Documents Configuration Warnings", report.ToString(), 35);
            severity = AnalysisSeverity.Warning;
        }
        else
        {
            fragment = ReportFragment.CreateSection("Documents Configuration", report.ToString(), 110);
            severity = AnalysisSeverity.None;
        }
        
        var result = AnalysisResult.CreateSuccess(Name, fragment);
        
        // Add metadata
        result.AddMetadata("HasOneDrive", !oneDriveCheck.isValid);
        result.AddMetadata("IniFilesValid", iniCheck.isValid);
        result.AddMetadata("PermissionsValid", permissionCheck.isValid);
        
        // Store documents path in context for other analyzers
        var docsPath = await GetDocumentsPathAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(docsPath))
        {
            context.SetSharedData("DocumentsPath", docsPath);
        }
        
        var finalResult = new AnalysisResult(result.AnalyzerName)
        {
            Success = result.Success,
            Fragment = result.Fragment,
            Severity = severity,
            Duration = result.Duration,
            SkipFurtherProcessing = result.SkipFurtherProcessing
        };
        
        // Copy errors and warnings
        foreach (var error in result.Errors)
            finalResult.AddError(error);
        foreach (var warning in result.Warnings)
            finalResult.AddWarning(warning);
        foreach (var kvp in result.Metadata)
            finalResult.AddMetadata(kvp.Key, kvp.Value);
            
        return finalResult;
    }
    
    private async Task<(bool isValid, string message)> CheckOneDriveConfigurationAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var docsPath = await GetDocumentsPathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(docsPath))
            {
                return (false, "⚠️ Documents path not configured");
            }
            
            // Check for OneDrive in path
            if (docsPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                return (false, 
                    "⚠️ Documents folder is synced with OneDrive\n" +
                    "   This can cause save game corruption and mod conflicts.\n" +
                    "   Consider excluding game folders from OneDrive sync.");
            }
            
            // Check for other cloud services
            if (docsPath.Contains("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                docsPath.Contains("Google Drive", StringComparison.OrdinalIgnoreCase) ||
                docsPath.Contains("iCloud", StringComparison.OrdinalIgnoreCase))
            {
                return (false, 
                    "⚠️ Documents folder is in a cloud storage location\n" +
                    "   This may cause synchronization conflicts with game files.");
            }
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check OneDrive configuration: {Message}", ex.Message);
            return (true, string.Empty);
        }
    }
    
    private async Task<(bool isValid, string message)> CheckIniFilesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var docsPath = await GetDocumentsPathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(docsPath))
            {
                return (false, string.Empty);
            }
            
            // Check for game INI files
            var iniFiles = new[]
            {
                "Fallout4.ini",
                "Fallout4Prefs.ini",
                "Fallout4Custom.ini"
            };
            
            var missingFiles = new List<string>();
            var readOnlyFiles = new List<string>();
            
            foreach (var iniFile in iniFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var fullPath = Path.Combine(docsPath, "My Games", "Fallout4", iniFile);
                
                if (!File.Exists(fullPath))
                {
                    // Only required files should be reported as missing
                    if (iniFile != "Fallout4Custom.ini")
                    {
                        missingFiles.Add(iniFile);
                    }
                }
                else
                {
                    // Check if file is read-only
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.IsReadOnly)
                    {
                        readOnlyFiles.Add(iniFile);
                    }
                }
            }
            
            var messages = new List<string>();
            
            if (missingFiles.Count > 0)
            {
                messages.Add($"❌ Missing INI files: {string.Join(", ", missingFiles)}");
            }
            
            if (readOnlyFiles.Count > 0)
            {
                messages.Add($"⚠️ Read-only INI files: {string.Join(", ", readOnlyFiles)}");
            }
            
            if (messages.Count > 0)
            {
                return (missingFiles.Count == 0, string.Join("\n", messages));
            }
            
            return (true, "✅ INI files are properly configured");
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check INI files: {Message}", ex.Message);
            return (true, string.Empty);
        }
    }
    
    private async Task<(bool isValid, string message)> CheckFolderPermissionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var docsPath = await GetDocumentsPathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(docsPath))
            {
                return (false, string.Empty);
            }
            
            var gameFolderPath = Path.Combine(docsPath, "My Games", "Fallout4");
            
            // Try to create and delete a test file to verify write permissions
            var testFilePath = Path.Combine(gameFolderPath, $"permission_test_{Guid.NewGuid()}.tmp");
            
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(gameFolderPath);
                
                // Test write permissions
                await File.WriteAllTextAsync(testFilePath, "test", cancellationToken).ConfigureAwait(false);
                File.Delete(testFilePath);
                
                return (true, "✅ Documents folder has proper write permissions");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "❌ No write permissions to documents folder");
            }
            catch (DirectoryNotFoundException)
            {
                return (false, "⚠️ Game documents folder does not exist");
            }
            finally
            {
                // Clean up test file if it exists
                if (File.Exists(testFilePath))
                {
                    try { File.Delete(testFilePath); } catch { /* Ignore cleanup errors */ }
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check folder permissions: {Message}", ex.Message);
            return (true, string.Empty);
        }
    }
    
    private async Task<string> GetDocumentsPathAsync()
    {
        // This would normally read from settings cache
        // For now, return the user's documents folder
        await Task.CompletedTask.ConfigureAwait(false);
        
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}