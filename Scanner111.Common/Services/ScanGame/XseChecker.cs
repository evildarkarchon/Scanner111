using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Checks the integrity and installation status of XSE (Script Extender) and related components.
/// </summary>
/// <remarks>
/// <para>
/// This service validates the XSE installation by checking Address Library installation,
/// XSE log file contents, version verification, and script file hash integrity.
/// </para>
/// <para>
/// The checker operates in read-only mode and never modifies any files.
/// </para>
/// </remarks>
public sealed class XseChecker : IXseChecker
{
    private readonly ILogger<XseChecker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XseChecker"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public XseChecker(ILogger<XseChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <inheritdoc/>
    public async Task<XseScanResult> CheckIntegrityAsync(
        XseConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        // Check Address Library
        var (addressLibInstalled, addressLibStatus) = await CheckAddressLibraryAsync(
            configuration.AddressLibraryPath,
            cancellationToken).ConfigureAwait(false);

        // Check XSE installation
        var (xseStatus, detectedVersion, isLatest) = await CheckXseInstallationAsync(
            configuration.LogFilePath,
            configuration.LatestVersion,
            cancellationToken).ConfigureAwait(false);

        var xseInstalled = xseStatus == XseInstallationStatus.Installed;

        // Check for errors in log file
        var logErrors = xseInstalled && !string.IsNullOrEmpty(configuration.LogFilePath)
            ? await ScanLogForErrorsAsync(
                configuration.LogFilePath,
                configuration.LogErrorPatterns,
                cancellationToken).ConfigureAwait(false)
            : Array.Empty<XseLogError>();

        // Verify script hashes
        var scriptHashResults = !string.IsNullOrEmpty(configuration.ScriptsFolderPath) &&
                                 configuration.ExpectedScriptHashes.Count > 0
            ? await VerifyScriptHashesAsync(
                configuration.ScriptsFolderPath,
                configuration.ExpectedScriptHashes,
                cancellationToken).ConfigureAwait(false)
            : Array.Empty<ScriptHashResult>();

        return new XseScanResult
        {
            AddressLibraryInstalled = addressLibInstalled,
            AddressLibraryStatus = addressLibStatus,
            XseInstalled = xseInstalled,
            XseStatus = xseStatus,
            DetectedVersion = detectedVersion,
            IsLatestVersion = isLatest,
            LogErrors = logErrors,
            ScriptHashResults = scriptHashResults
        };
    }

    /// <inheritdoc/>
    public Task<(bool IsInstalled, AddressLibraryStatus Status)> CheckAddressLibraryAsync(
        string? addressLibraryPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(addressLibraryPath))
        {
            return Task.FromResult((false, AddressLibraryStatus.InvalidConfiguration));
        }

        if (File.Exists(addressLibraryPath))
        {
            return Task.FromResult((true, AddressLibraryStatus.Installed));
        }

        return Task.FromResult((false, AddressLibraryStatus.Missing));
    }

    /// <inheritdoc/>
    public async Task<(XseInstallationStatus Status, string? DetectedVersion, bool IsLatest)> CheckXseInstallationAsync(
        string? logFilePath,
        string latestVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return (XseInstallationStatus.InvalidConfiguration, null, false);
        }

        if (!File.Exists(logFilePath))
        {
            return (XseInstallationStatus.LogFileMissing, null, false);
        }

        // XSE is installed - read the first line to get version
        string? firstLine = null;
        try
        {
            await using var stream = new FileStream(
                logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(stream);
            firstLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read XSE log file: {LogFilePath}", logFilePath);
            return (XseInstallationStatus.Installed, null, false);
        }

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return (XseInstallationStatus.Installed, null, false);
        }

        // Extract version from first line
        var detectedVersion = ExtractVersionFromLogLine(firstLine);
        var isLatest = !string.IsNullOrEmpty(latestVersion) &&
                       firstLine.Contains(latestVersion, StringComparison.OrdinalIgnoreCase);

        return (XseInstallationStatus.Installed, detectedVersion, isLatest);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<XseLogError>> ScanLogForErrorsAsync(
        string logFilePath,
        IReadOnlyList<string> errorPatterns,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(logFilePath) || errorPatterns.Count == 0)
        {
            return Array.Empty<XseLogError>();
        }

        var errors = new List<XseLogError>();

        try
        {
            var lines = await File.ReadAllLinesAsync(logFilePath, cancellationToken).ConfigureAwait(false);

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[lineNumber];
                foreach (var pattern in errorPatterns)
                {
                    if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new XseLogError(
                            LineNumber: lineNumber + 1,
                            ErrorText: line.Trim(),
                            MatchedPattern: pattern));
                        break; // Only report one pattern match per line
                    }
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to scan XSE log for errors: {LogFilePath}", logFilePath);
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScriptHashResult>> VerifyScriptHashesAsync(
        string scriptsFolderPath,
        IReadOnlyDictionary<string, string> expectedHashes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptsFolderPath) || expectedHashes.Count == 0)
        {
            return Array.Empty<ScriptHashResult>();
        }

        var results = new List<ScriptHashResult>();

        foreach (var (fileName, expectedHash) in expectedHashes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(scriptsFolderPath, fileName);

            if (!File.Exists(filePath))
            {
                results.Add(new ScriptHashResult(
                    FileName: fileName,
                    ExpectedHash: expectedHash,
                    ActualHash: null,
                    Status: ScriptHashStatus.Missing));
                continue;
            }

            try
            {
                var actualHash = await ComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);
                var status = string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)
                    ? ScriptHashStatus.Valid
                    : ScriptHashStatus.Mismatch;

                results.Add(new ScriptHashResult(
                    FileName: fileName,
                    ExpectedHash: expectedHash,
                    ActualHash: actualHash,
                    Status: status));
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to read script file for hash verification: {FilePath}", filePath);
                results.Add(new ScriptHashResult(
                    FileName: fileName,
                    ExpectedHash: expectedHash,
                    ActualHash: null,
                    Status: ScriptHashStatus.ReadError));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to script file: {FilePath}", filePath);
                results.Add(new ScriptHashResult(
                    FileName: fileName,
                    ExpectedHash: expectedHash,
                    ActualHash: null,
                    Status: ScriptHashStatus.ReadError));
            }
        }

        return results;
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Extracts version information from an XSE log file's first line.
    /// </summary>
    /// <remarks>
    /// XSE log files typically start with a line like:
    /// "F4SE runtime: 0.6.23, release 0 64-bit" or similar format.
    /// </remarks>
    private static string? ExtractVersionFromLogLine(string line)
    {
        // Common patterns in XSE log first lines:
        // "F4SE runtime: 0.6.23, release 0 64-bit"
        // "SKSE64 runtime: 2.2.6"
        // Try to extract the version number

        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0 || colonIndex >= line.Length - 1)
        {
            return null;
        }

        var afterColon = line[(colonIndex + 1)..].Trim();

        // Find the version pattern (digits and dots)
        var versionStart = -1;
        var versionEnd = -1;

        for (var i = 0; i < afterColon.Length; i++)
        {
            var c = afterColon[i];
            if (char.IsDigit(c) || c == '.')
            {
                if (versionStart < 0)
                {
                    versionStart = i;
                }
                versionEnd = i + 1;
            }
            else if (versionStart >= 0)
            {
                break;
            }
        }

        if (versionStart >= 0 && versionEnd > versionStart)
        {
            var version = afterColon[versionStart..versionEnd];
            // Ensure it looks like a version (has at least one dot and digits)
            if (version.Contains('.') && version.Any(char.IsDigit))
            {
                return version;
            }
        }

        return null;
    }
}
