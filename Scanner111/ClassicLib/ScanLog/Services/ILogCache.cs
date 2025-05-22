using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.ClassicLib.ScanLog.Services;

/// <summary>
/// Thread-safe in-memory cache for crash log files.
/// Equivalent to Python's ThreadSafeLogCache class.
/// </summary>
public interface ILogCache : IDisposable
{
    /// <summary>
    /// Reads log data from the cache for the given log name.
    /// </summary>
    /// <param name="logName">The name of the log file.</param>
    /// <returns>A list of log lines.</returns>
    Task<List<string>> ReadLogAsync(string logName);

    /// <summary>
    /// Gets all log names in the cache.
    /// </summary>
    /// <returns>A list of log names.</returns>
    List<string> GetLogNames();

    /// <summary>
    /// Adds a new log to the cache.
    /// </summary>
    /// <param name="path">Path to the log file.</param>
    /// <returns>True if added successfully, false otherwise.</returns>
    Task<bool> AddLogAsync(string path);
}

/// <summary>
/// Thread-safe implementation of log cache using concurrent collections.
/// </summary>
public class ThreadSafeLogCache : ILogCache
{
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes the cache with the provided log files.
    /// </summary>
    /// <param name="logFiles">List of log file paths.</param>
    public ThreadSafeLogCache(IEnumerable<string> logFiles)
    {
        foreach (var file in logFiles)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var content = File.ReadAllBytes(file);
                _cache.TryAdd(fileName, content);
            }
            catch (Exception ex)
            {
                // Log the error but continue processing other files
                Console.WriteLine($"Error reading {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Thread-safely reads log data from the cache.
    /// </summary>
    /// <param name="logName">The name of the log file.</param>
    /// <returns>A list of log lines.</returns>
    public Task<List<string>> ReadLogAsync(string logName)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_cache.TryGetValue(logName, out var logData))
                {
                    return new List<string>();
                }

                var content = Encoding.UTF8.GetString(logData);
                var lines = content.Split('\n', '\r');
                var result = new List<string>();

                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line.Trim()))
                    {
                        result.Add(line);
                    }
                }

                return result;
            }
        });
    }

    /// <summary>
    /// Gets all log names in the cache.
    /// </summary>
    /// <returns>A list of log names.</returns>
    public List<string> GetLogNames()
    {
        lock (_lock)
        {
            return new List<string>(_cache.Keys);
        }
    }

    /// <summary>
    /// Adds a new log to the cache.
    /// </summary>
    /// <param name="path">Path to the log file.</param>
    /// <returns>True if added successfully, false otherwise.</returns>
    public async Task<bool> AddLogAsync(string path)
    {
        try
        {
            var fileName = Path.GetFileName(path);
            var content = await File.ReadAllBytesAsync(path);

            lock (_lock)
            {
                return _cache.TryAdd(fileName, content);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _cache.Clear();
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
