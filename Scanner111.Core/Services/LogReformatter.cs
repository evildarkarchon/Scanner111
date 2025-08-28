using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.IO;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for reformatting crash log files with various normalizations and simplifications.
///     Provides functionality similar to Python's AsyncReformat module.
/// </summary>
public sealed partial class LogReformatter : IAsyncDisposable
{
    private readonly ILogger<LogReformatter> _logger;
    private readonly IAsyncYamlSettingsCore _settingsCore;
    private readonly IFileIoCore? _fileIoCore;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ArrayPool<char> _charPool;
    private readonly Lazy<Regex> _bracketRegex;
    private readonly int _batchSize;
    private bool _disposed;

    public LogReformatter(
        ILogger<LogReformatter> logger,
        IAsyncYamlSettingsCore settingsCore,
        IFileIoCore? fileIoCore = null,
        int maxConcurrency = 20)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsCore = settingsCore ?? throw new ArgumentNullException(nameof(settingsCore));
        _fileIoCore = fileIoCore;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
        _charPool = ArrayPool<char>.Shared;
        _bracketRegex = new Lazy<Regex>(() => BracketPattern());
        _batchSize = maxConcurrency;
    }

    /// <summary>
    ///     Reformats a single log file based on configured parameters.
    /// </summary>
    /// <param name="filePath">Path to the log file to reformat.</param>
    /// <param name="removePatterns">Patterns to remove when simplifying logs.</param>
    /// <param name="simplifyLogs">Whether to remove lines containing specified patterns.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReformatSingleLogAsync(
        string filePath,
        IReadOnlyList<string>? removePatterns = null,
        bool simplifyLogs = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            _logger.LogDebug("Reformatting log file: {FilePath}", filePath);

            // Read file content
            string content;
            if (_fileIoCore != null)
            {
                content = await _fileIoCore.ReadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }

            var lines = content.Split('\n', StringSplitOptions.None);
            var processedLines = new List<string>();
            var inPluginsSection = false;

            // Process lines from top to bottom (unlike Python which processes in reverse)
            // We'll detect the PLUGINS section and process accordingly
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Check if we're entering the PLUGINS section
                if (line.StartsWith("PLUGINS:", StringComparison.OrdinalIgnoreCase))
                {
                    inPluginsSection = true;
                    processedLines.Add(line);
                    continue;
                }

                // Check if we're exiting the PLUGINS section (empty line or new section)
                if (inPluginsSection && (string.IsNullOrWhiteSpace(line) || 
                    (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line.Contains(':'))))
                {
                    inPluginsSection = false;
                }

                // Skip lines if simplifying logs and pattern matches
                if (simplifyLogs && removePatterns != null && removePatterns.Count > 0)
                {
                    bool shouldRemove = false;
                    foreach (var pattern in removePatterns)
                    {
                        if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldRemove = true;
                            break;
                        }
                    }

                    if (shouldRemove)
                    {
                        _logger.LogTrace("Removing line matching pattern: {Line}", line);
                        continue;
                    }
                }

                // Reformat lines within PLUGINS section
                if (inPluginsSection && line.Contains('[') && line.Contains(']'))
                {
                    line = ReformatPluginLine(line);
                }

                processedLines.Add(line);
            }

            // Write back the reformatted content
            var reformattedContent = string.Join('\n', processedLines);
            
            if (_fileIoCore != null)
            {
                await _fileIoCore.WriteFileAsync(filePath, reformattedContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, reformattedContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Successfully reformatted {FileName}", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reformatting file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    ///     Reformats multiple crash log files in batches for optimal performance.
    /// </summary>
    /// <param name="crashLogPaths">List of crash log file paths to process.</param>
    /// <param name="removePatterns">Patterns to remove when simplifying logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReformatCrashLogsAsync(
        IReadOnlyList<string> crashLogPaths,
        IReadOnlyList<string>? removePatterns = null,
        CancellationToken cancellationToken = default)
    {
        if (crashLogPaths == null || crashLogPaths.Count == 0)
        {
            _logger.LogDebug("No crash logs to reformat");
            return;
        }

        _logger.LogInformation("Starting async crash log file reformat for {Count} files", crashLogPaths.Count);

        // Get simplify logs setting
        bool simplifyLogs = false;
        try
        {
            var simplifyLogsValue = await _settingsCore.GetSettingAsync<string>(
                YamlStore.Settings,
                "SimplifyLogs",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (!string.IsNullOrWhiteSpace(simplifyLogsValue) && 
                (simplifyLogsValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                 simplifyLogsValue.Equals("1", StringComparison.Ordinal)))
            {
                simplifyLogs = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read SimplifyLogs setting, using default (false)");
        }

        // Process in batches to avoid overwhelming the file system
        for (int i = 0; i < crashLogPaths.Count; i += _batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Reformat operation cancelled");
                break;
            }

            var batchEnd = Math.Min(i + _batchSize, crashLogPaths.Count);
            var batch = crashLogPaths.Skip(i).Take(batchEnd - i).ToList();

            _logger.LogDebug("Processing batch {BatchNumber}/{TotalBatches}", 
                (i / _batchSize) + 1, 
                (crashLogPaths.Count + _batchSize - 1) / _batchSize);

            // Process batch concurrently with semaphore limiting
            var tasks = batch.Select(async path =>
            {
                await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ReformatSingleLogAsync(path, removePatterns, simplifyLogs, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _concurrencyLimiter.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Small delay between batches to avoid file system overload
            if (batchEnd < crashLogPaths.Count)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Completed async crash log file reformat");
    }

    /// <summary>
    ///     Reformats a plugin line to normalize the load order format.
    ///     Replaces spaces inside brackets with zeros for consistency.
    /// </summary>
    /// <param name="line">The plugin line to reformat.</param>
    /// <returns>The reformatted line.</returns>
    private string ReformatPluginLine(string line)
    {
        try
        {
            // Find the bracket content
            var startBracket = line.IndexOf('[');
            var endBracket = line.IndexOf(']', startBracket + 1);

            if (startBracket >= 0 && endBracket > startBracket)
            {
                var indent = line.Substring(0, startBracket);
                var bracketContent = line.Substring(startBracket + 1, endBracket - startBracket - 1);
                var remainder = line.Substring(endBracket + 1);

                // Replace spaces with zeros in the bracket content
                bracketContent = bracketContent.Replace(' ', '0');

                return $"{indent}[{bracketContent}]{remainder}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to reformat plugin line: {Line}", line);
        }

        // Return original line if reformatting fails
        return line;
    }

    /// <summary>
    ///     Performs batch file move operations asynchronously.
    /// </summary>
    /// <param name="operations">List of source and destination path tuples.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BatchFileMoveAsync(
        IReadOnlyList<(string Source, string Destination)> operations,
        CancellationToken cancellationToken = default)
    {
        if (operations == null || operations.Count == 0)
        {
            _logger.LogDebug("No file move operations to perform");
            return;
        }

        _logger.LogDebug("Performing {Count} file move operations", operations.Count);

        var tasks = operations.Select(async op =>
        {
            await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => File.Move(op.Source, op.Destination, true), cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogTrace("Moved {Source} to {Destination}", 
                    Path.GetFileName(op.Source), 
                    Path.GetFileName(op.Destination));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving {Source} to {Destination}", op.Source, op.Destination);
                throw;
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs batch file copy operations asynchronously.
    /// </summary>
    /// <param name="operations">List of source and destination path tuples.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BatchFileCopyAsync(
        IReadOnlyList<(string Source, string Destination)> operations,
        CancellationToken cancellationToken = default)
    {
        if (operations == null || operations.Count == 0)
        {
            _logger.LogDebug("No file copy operations to perform");
            return;
        }

        _logger.LogDebug("Performing {Count} file copy operations", operations.Count);

        var tasks = operations.Select(async op =>
        {
            await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => File.Copy(op.Source, op.Destination, true), cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogTrace("Copied {Source} to {Destination}", 
                    Path.GetFileName(op.Source), 
                    Path.GetFileName(op.Destination));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying {Source} to {Destination}", op.Source, op.Destination);
                throw;
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    ///     Generated regex for bracket pattern matching.
    /// </summary>
    [GeneratedRegex(@"\[([^\]]*)\]", RegexOptions.Compiled)]
    private static partial Regex BracketPattern();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _concurrencyLimiter?.Dispose();
        _disposed = true;

        await ValueTask.CompletedTask;
    }
}