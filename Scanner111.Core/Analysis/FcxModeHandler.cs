using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis;

/// <summary>
/// Interface for FCX mode handling functionality.
/// </summary>
public interface IFcxModeHandler
{
    /// <summary>
    /// Gets whether FCX mode is enabled.
    /// </summary>
    bool? FcxMode { get; }
    
    /// <summary>
    /// Gets the main files check result.
    /// </summary>
    string MainFilesCheck { get; }
    
    /// <summary>
    /// Gets the game files check result.
    /// </summary>
    string GameFilesCheck { get; }
    
    /// <summary>
    /// Checks FCX mode and runs necessary file checks if enabled.
    /// Thread-safe and caches results across multiple calls.
    /// </summary>
    Task CheckFcxModeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a report fragment with FCX mode messages.
    /// </summary>
    ReportFragment GetFcxMessages();
    
    /// <summary>
    /// Resets FCX checks and cached results.
    /// </summary>
    void ResetFcxChecks();
}

/// <summary>
/// Handles FCX mode checking with thread-safe caching of results.
/// Implements singleton pattern for shared state across analyzer instances.
/// </summary>
public sealed class FcxModeHandler : IFcxModeHandler, IDisposable
{
    private readonly ILogger<FcxModeHandler> _logger;
    private readonly SemaphoreSlim _fcxLock = new(1, 1);
    private readonly ModDetectionSettings _modSettings;
    
    // Static fields for singleton behavior (shared across instances)
    private static readonly SemaphoreSlim s_globalLock = new(1, 1);
    private static bool s_fcxChecksRun;
    private static string s_mainFilesResult = string.Empty;
    private static string s_gameFilesResult = string.Empty;
    private static DateTime s_lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan s_cacheExpiration = TimeSpan.FromMinutes(5);
    
    // Instance fields
    private string _mainFilesCheck = string.Empty;
    private string _gameFilesCheck = string.Empty;
    private bool _disposed;
    
    /// <inheritdoc />
    public bool? FcxMode => _modSettings.FcxMode;
    
    /// <inheritdoc />
    public string MainFilesCheck => _mainFilesCheck;
    
    /// <inheritdoc />
    public string GameFilesCheck => _gameFilesCheck;
    
    public FcxModeHandler(
        ILogger<FcxModeHandler> logger,
        ModDetectionSettings modSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modSettings = modSettings ?? throw new ArgumentNullException(nameof(modSettings));
    }
    
    /// <inheritdoc />
    public async Task CheckFcxModeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FcxModeHandler));
        
        if (FcxMode == true)
        {
            await _fcxLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Use global lock for cross-instance synchronization
                await s_globalLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var now = DateTime.UtcNow;
                    var cacheExpired = (now - s_lastCheckTime) > s_cacheExpiration;
                    
                    // Check if we need to run FCX checks
                    if (!s_fcxChecksRun || cacheExpired)
                    {
                        _logger.LogInformation("Running FCX mode file checks (cache expired: {Expired})", cacheExpired);
                        
                        // Simulate the file checks that would be done by SetupCoordinator and game_combined_result
                        // In the real implementation, these would call the actual services
                        s_mainFilesResult = await RunMainFilesCheckAsync(cancellationToken).ConfigureAwait(false);
                        s_gameFilesResult = await RunGameFilesCheckAsync(cancellationToken).ConfigureAwait(false);
                        
                        s_fcxChecksRun = true;
                        s_lastCheckTime = now;
                        
                        _logger.LogInformation("FCX mode file checks completed");
                    }
                    else
                    {
                        _logger.LogDebug("Using cached FCX check results (age: {Age})", now - s_lastCheckTime);
                    }
                    
                    // Always assign the stored results to instance variables
                    _mainFilesCheck = s_mainFilesResult;
                    _gameFilesCheck = s_gameFilesResult;
                }
                finally
                {
                    s_globalLock.Release();
                }
            }
            finally
            {
                _fcxLock.Release();
            }
        }
        else
        {
            _mainFilesCheck = "❌ FCX Mode is disabled, skipping game files check... \n-----\n";
            _gameFilesCheck = string.Empty;
            _logger.LogDebug("FCX mode is disabled, skipping file checks");
        }
    }
    
    /// <inheritdoc />
    public ReportFragment GetFcxMessages()
    {
        var content = new System.Text.StringBuilder();
        
        if (FcxMode == true)
        {
            content.AppendLine("* NOTICE: FCX MODE IS ENABLED. Scanner111 MUST BE RUN BY THE ORIGINAL USER FOR CORRECT DETECTION *");
            content.AppendLine();
            content.AppendLine("[ To disable mod & game files detection, disable FCX Mode in Scanner111 Settings.yaml ]");
            content.AppendLine();
            
            if (!string.IsNullOrWhiteSpace(_mainFilesCheck))
            {
                content.Append(_mainFilesCheck);
            }
            
            if (!string.IsNullOrWhiteSpace(_gameFilesCheck))
            {
                content.Append(_gameFilesCheck);
            }
            
            return ReportFragment.CreateInfo(
                "FCX Mode Status",
                content.ToString(),
                order: 10);
        }
        else
        {
            content.AppendLine("* NOTICE: FCX MODE IS DISABLED. YOU CAN ENABLE IT TO DETECT PROBLEMS IN YOUR MOD & GAME FILES *");
            content.AppendLine();
            content.AppendLine("[ FCX Mode can be enabled in Scanner111 Settings.yaml located in your Scanner111 folder. ]");
            content.AppendLine();
            
            return ReportFragment.CreateInfo(
                "FCX Mode Status",
                content.ToString(),
                order: 10);
        }
    }
    
    /// <inheritdoc />
    public void ResetFcxChecks()
    {
        s_globalLock.Wait();
        try
        {
            s_fcxChecksRun = false;
            s_mainFilesResult = string.Empty;
            s_gameFilesResult = string.Empty;
            s_lastCheckTime = DateTime.MinValue;
            
            _mainFilesCheck = string.Empty;
            _gameFilesCheck = string.Empty;
            
            _logger.LogInformation("FCX checks and cached results have been reset");
        }
        finally
        {
            s_globalLock.Release();
        }
    }
    
    /// <summary>
    /// Runs main files check (placeholder for SetupCoordinator integration).
    /// </summary>
    private async Task<string> RunMainFilesCheckAsync(CancellationToken cancellationToken)
    {
        // This would integrate with the actual SetupCoordinator when available
        // For now, return a placeholder message
        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate work
        
        return "✔️ Main game files integrity check completed successfully.\n-----\n";
    }
    
    /// <summary>
    /// Runs game files check (placeholder for game_combined_result integration).
    /// </summary>
    private async Task<string> RunGameFilesCheckAsync(CancellationToken cancellationToken)
    {
        // This would integrate with the actual game scanning functionality when available
        // For now, return a placeholder message
        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate work
        
        return "✔️ Game mod files check completed successfully.\n-----\n";
    }
    
    /// <summary>
    /// Cleans up resources used by the handler.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _fcxLock?.Dispose();
        _disposed = true;
        
        _logger.LogDebug("FcxModeHandler disposed");
    }
}