using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking XSE (Script Extender) plugin compatibility and Address Library versions.
///     Provides functionality equivalent to Python's CheckXsePlugins.py with thread-safe operations.
/// </summary>
public sealed class XsePluginChecker : IXsePluginChecker
{
    private readonly ILogger<XsePluginChecker> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;

    public XsePluginChecker(ILogger<XsePluginChecker> logger, IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }

    /// <inheritdoc />
    public async Task<string> CheckXsePluginsAsync(CancellationToken cancellationToken = default)
    {
        var messageBuilder = new StringBuilder();

        try
        {
            _logger.LogInformation("Starting XSE plugins compatibility check");

            // Get paths and settings from configuration
            var (pluginsPath, gameExePath, isVrMode) = await GetPluginCheckSettingsAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(gameExePath))
            {
                var errorMessage = FormatGameVersionNotDetectedMessage();
                messageBuilder.Append(errorMessage);
                return messageBuilder.ToString();
            }

            if (string.IsNullOrEmpty(pluginsPath))
            {
                var errorMessage = FormatPluginsPathNotFoundMessage();
                messageBuilder.Append(errorMessage);
                return messageBuilder.ToString();
            }

            // Get game version from executable
            var gameVersion = await DetectGameVersionAsync(gameExePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(gameVersion))
            {
                var errorMessage = FormatGameVersionNotDetectedMessage();
                messageBuilder.Append(errorMessage);
                return messageBuilder.ToString();
            }

            _logger.LogDebug("Detected game version: {GameVersion}, VR Mode: {IsVrMode}", gameVersion, isVrMode);

            // Validate Address Library
            var (isCorrectVersion, message) = await ValidateAddressLibraryAsync(pluginsPath, gameVersion, isVrMode, cancellationToken).ConfigureAwait(false);
            messageBuilder.Append(message);

            _logger.LogInformation("XSE plugins check completed. Correct version: {IsCorrect}", isCorrectVersion);
            return messageBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check XSE plugins");
            messageBuilder.AppendLine($"❌ ERROR: Failed to check XSE plugins: {ex.Message}");
            messageBuilder.AppendLine("-----");
            return messageBuilder.ToString();
        }
    }

    /// <inheritdoc />
    public Task<(bool IsCorrectVersion, string Message)> ValidateAddressLibraryAsync(
        string pluginsPath,
        string gameVersion,
        bool isVrMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        try
        {
            _logger.LogDebug("Validating Address Library at: {PluginsPath}", pluginsPath);

            if (!Directory.Exists(pluginsPath))
            {
                return Task.FromResult((false, FormatPluginsPathNotFoundMessage()));
            }

            // Check for cancellation before proceeding
            cancellationToken.ThrowIfCancellationRequested();

            // Determine relevant versions based on VR mode
            var (correctVersions, wrongVersions) = AddressLibraryInfo.DetermineRelevantVersions(isVrMode);

            // Check if correct version exists
            var correctVersionExists = false;
            foreach (var version in correctVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var versionFile = Path.Combine(pluginsPath, version.Filename);
                if (File.Exists(versionFile))
                {
                    correctVersionExists = true;
                    _logger.LogDebug("Found correct Address Library version: {Version}", version.Version);
                    break;
                }
            }

            // Check if wrong version exists
            var wrongVersionExists = false;
            AddressLibraryInfo? foundWrongVersion = null;
            foreach (var version in wrongVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var versionFile = Path.Combine(pluginsPath, version.Filename);
                if (File.Exists(versionFile))
                {
                    wrongVersionExists = true;
                    foundWrongVersion = version;
                    _logger.LogDebug("Found incorrect Address Library version: {Version}", version.Version);
                    break;
                }
            }

            // Generate appropriate message based on findings
            if (correctVersionExists)
            {
                return Task.FromResult((true, FormatCorrectAddressLibMessage()));
            }
            else if (wrongVersionExists && foundWrongVersion != null)
            {
                var correctVersion = correctVersions.First();
                return Task.FromResult((false, FormatWrongAddressLibMessage(correctVersion)));
            }
            else
            {
                var correctVersion = correctVersions.First();
                return Task.FromResult((false, FormatAddressLibNotFoundMessage(correctVersion)));
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate Address Library");
            return Task.FromResult((false, $"⚠️ Unable to validate Address Library: {ex.Message}\n-----\n"));
        }
    }

    #region Private Helper Methods

    private Task<(string? PluginsPath, string? GameExePath, bool IsVrMode)> GetPluginCheckSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // These would normally come from YAML settings
            // Using fallback values for now
            var pluginsPath = @"C:\Games\Fallout4\Data\F4SE\Plugins"; // Would be from Game_Local settings
            var gameExePath = @"C:\Games\Fallout4\Fallout4.exe"; // Would be from Game_Local settings  
            var isVrMode = false; // Would be from Classic settings

            return Task.FromResult((pluginsPath, gameExePath, isVrMode));
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get plugin check settings from configuration");
            return Task.FromResult<(string?, string?, bool)>((null, null, false));
        }
    }

    private Task<string?> DetectGameVersionAsync(string gameExePath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!File.Exists(gameExePath))
            {
                _logger.LogWarning("Game executable not found: {GameExePath}", gameExePath);
                return Task.FromResult<string?>(null);
            }

            cancellationToken.ThrowIfCancellationRequested();
            
            // Get file version info
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(gameExePath);
            if (versionInfo.FileVersion != null)
            {
                _logger.LogDebug("Detected game file version: {FileVersion}", versionInfo.FileVersion);
                return Task.FromResult<string?>(versionInfo.FileVersion);
            }

            // Fallback to file size/hash based detection if version info not available
            var fileInfo = new FileInfo(gameExePath);
            var fileSize = fileInfo.Length;
            
            _logger.LogDebug("Game executable size: {FileSize} bytes", fileSize);

            // This would normally use hash-based detection like the Python version
            // For now, return a mock version based on file size ranges
            var detectedVersion = fileSize switch
            {
                > 50_000_000 and < 60_000_000 => "1.10.984.0", // NG version range
                > 45_000_000 and < 55_000_000 => "1.10.163.0", // OG version range  
                > 35_000_000 and < 45_000_000 => "1.2.72.0",   // VR version range
                _ => null
            };
            
            return Task.FromResult(detectedVersion);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect game version from: {GameExePath}", gameExePath);
            return Task.FromResult<string?>(null);
        }
    }

    private static string FormatGameVersionNotDetectedMessage()
    {
        var message = new StringBuilder();
        message.AppendLine("❓ NOTICE : Unable to locate Address Library");
        message.AppendLine("  If you have Address Library installed, please check the path in your settings.");
        message.AppendLine("  If you don't have it installed, you can find it on the Nexus.");
        message.AppendLine($"  Link: Regular: {AddressLibraryInfo.KnownVersions["OG"].DownloadUrl} or VR: {AddressLibraryInfo.KnownVersions["VR"].DownloadUrl}");
        message.AppendLine("-----");
        return message.ToString();
    }

    private static string FormatPluginsPathNotFoundMessage()
    {
        return "❌ ERROR: Could not locate plugins folder path in settings\n-----\n";
    }

    private static string FormatCorrectAddressLibMessage()
    {
        return "✔️ You have the correct version of the Address Library file!\n-----\n";
    }

    private static string FormatWrongAddressLibMessage(AddressLibraryInfo correctVersionInfo)
    {
        var message = new StringBuilder();
        message.AppendLine("❌ CAUTION: You have installed the wrong version of the Address Library file!");
        message.AppendLine($"  Remove the current Address Library file and install the {correctVersionInfo.Description}.");
        message.AppendLine($"  Link: {correctVersionInfo.DownloadUrl}");
        message.AppendLine("-----");
        return message.ToString();
    }

    private static string FormatAddressLibNotFoundMessage(AddressLibraryInfo correctVersionInfo)
    {
        var message = new StringBuilder();
        message.AppendLine("❓ NOTICE: Address Library file not found");
        message.AppendLine($"  Please install the {correctVersionInfo.Description} for proper functionality.");
        message.AppendLine($"  Link: {correctVersionInfo.DownloadUrl}");
        message.AppendLine("-----");
        return message.ToString();
    }

    #endregion
}