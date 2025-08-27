using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes game installation integrity and version status.
/// </summary>
public sealed class GameIntegrityAnalyzer : AnalyzerBase
{
    private readonly IAsyncYamlSettingsCore _yamlCore;
    
    public GameIntegrityAnalyzer(
        ILogger<GameIntegrityAnalyzer> logger,
        IAsyncYamlSettingsCore yamlCore) : base(logger)
    {
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }
    
    public override string Name => "GameIntegrity";
    
    public override string DisplayName => "Game Integrity Check";
    
    public override int Priority => 10; // Run early to validate game installation
    
    public override TimeSpan Timeout => TimeSpan.FromSeconds(10);
    
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting game integrity analysis");
        
        var report = new StringBuilder();
        var hasErrors = false;
        var hasWarnings = false;
        
        // Check executable version
        var exeCheckResult = await CheckExecutableVersionAsync(cancellationToken).ConfigureAwait(false);
        report.AppendLine(exeCheckResult.message);
        
        if (!exeCheckResult.isValid)
        {
            hasWarnings = true;
        }
        
        // Check installation location
        var locationCheckResult = await CheckInstallationLocationAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(locationCheckResult.message))
        {
            report.AppendLine(locationCheckResult.message);
        }
        
        if (!locationCheckResult.isValid)
        {
            hasWarnings = true;
        }
        
        // Check for missing files
        var filesCheckResult = await CheckRequiredFilesAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(filesCheckResult.message))
        {
            report.AppendLine(filesCheckResult.message);
        }
        
        if (!filesCheckResult.isValid)
        {
            hasErrors = true;
        }
        
        // Create appropriate fragment based on results
        ReportFragment fragment;
        AnalysisSeverity severity;
        
        if (hasErrors)
        {
            fragment = ReportFragment.CreateError("Game Integrity Issues", report.ToString(), 20);
            severity = AnalysisSeverity.Error;
        }
        else if (hasWarnings)
        {
            fragment = ReportFragment.CreateWarning("Game Integrity Warnings", report.ToString(), 30);
            severity = AnalysisSeverity.Warning;
        }
        else
        {
            fragment = ReportFragment.CreateSection("Game Integrity", report.ToString(), 100);
            severity = AnalysisSeverity.None;
        }
        
        var result = AnalysisResult.CreateSuccess(Name, fragment);
        
        // Add metadata
        result.AddMetadata("HasErrors", hasErrors);
        result.AddMetadata("HasWarnings", hasWarnings);
        result.AddMetadata("ExecutableValid", exeCheckResult.isValid);
        result.AddMetadata("LocationValid", locationCheckResult.isValid);
        result.AddMetadata("RequiredFilesValid", filesCheckResult.isValid);
        
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
    
    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        // This analyzer can run on any analysis type
        await Task.CompletedTask.ConfigureAwait(false);
        return context != null;
    }
    
    private async Task<(bool isValid, string message)> CheckExecutableVersionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Get settings from cache (simulated for now)
            var gamePath = await GetGamePathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(gamePath))
            {
                return (false, "❌ Game executable path not configured");
            }
            
            if (!File.Exists(gamePath))
            {
                return (false, $"❌ Game executable not found at: {gamePath}");
            }
            
            // Calculate hash of executable
            var hash = await CalculateFileHashAsync(gamePath, cancellationToken).ConfigureAwait(false);
            
            // Check against known hashes (simulated)
            var knownHashes = await GetKnownHashesAsync().ConfigureAwait(false);
            
            if (knownHashes.Contains(hash))
            {
                return (true, "✅ Game executable version is up to date");
            }
            
            return (false, "⚠️ Game executable version may be outdated");
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check executable version: {Message}", ex.Message);
            return (false, "⚠️ Unable to verify game executable version");
        }
    }
    
    private async Task<(bool isValid, string message)> CheckInstallationLocationAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var gamePath = await GetGamePathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(gamePath))
            {
                return (false, string.Empty);
            }
            
            // Check if installed in Program Files (not recommended)
            if (gamePath.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "⚠️ Game is installed in Program Files - this may cause permission issues");
            }
            
            // Check for OneDrive or cloud storage paths
            if (gamePath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                gamePath.Contains("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                gamePath.Contains("Google Drive", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "⚠️ Game is installed in a cloud storage folder - this may cause sync issues");
            }
            
            return (true, "✅ Game installation location is optimal");
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check installation location: {Message}", ex.Message);
            return (true, string.Empty);
        }
    }
    
    private async Task<(bool isValid, string message)> CheckRequiredFilesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var gamePath = await GetGamePathAsync().ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(gamePath))
            {
                return (false, string.Empty);
            }
            
            var gameDir = Path.GetDirectoryName(gamePath);
            if (string.IsNullOrEmpty(gameDir))
            {
                return (false, string.Empty);
            }
            
            // Check for required files (example list)
            var requiredFiles = new[]
            {
                "Data",
                "Meshes",
                "Textures",
                "Scripts"
            };
            
            var missingDirectories = new List<string>();
            
            foreach (var dir in requiredFiles)
            {
                var fullPath = Path.Combine(gameDir, dir);
                if (!Directory.Exists(fullPath))
                {
                    missingDirectories.Add(dir);
                }
                
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            if (missingDirectories.Count > 0)
            {
                return (false, $"❌ Missing required directories: {string.Join(", ", missingDirectories)}");
            }
            
            return (true, "✅ All required game files are present");
        }
        catch (Exception ex)
        {
            LogWarning("Failed to check required files: {Message}", ex.Message);
            return (true, string.Empty);
        }
    }
    
    private async Task<string> GetGamePathAsync()
    {
        // This would normally read from settings cache
        // For now, return a simulated path
        await Task.CompletedTask.ConfigureAwait(false);
        
        // Try to get from context or settings
        return @"C:\Games\Fallout4\Fallout4.exe"; // Example path
    }
    
    private async Task<string[]> GetKnownHashesAsync()
    {
        // This would normally read from settings cache
        await Task.CompletedTask.ConfigureAwait(false);
        
        return new[]
        {
            "ABC123DEF456", // Example hashes
            "789GHI012JKL"
        };
    }
    
    private async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }
        
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        
        var hash = sha256.Hash;
        if (hash == null)
            return string.Empty;
        
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }
}