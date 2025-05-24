using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner111.Services.Interfaces;

/// <summary>
///     Thread-safe in-memory cache for crash log files.
///     Equivalent to Python's ThreadSafeLogCache class.
/// </summary>
public interface ILogCache : IDisposable
{
    /// <summary>
    ///     Reads log data from the cache for the given log name.
    /// </summary>
    /// <param name="logName">The name of the log file.</param>
    /// <returns>A list of log lines.</returns>
    Task<List<string>> ReadLogAsync(string logName);

    /// <summary>
    ///     Gets all log names in the cache.
    /// </summary>
    /// <returns>A list of log names.</returns>
    List<string> GetLogNames();

    /// <summary>
    ///     Adds a new log to the cache.
    /// </summary>
    /// <param name="path">Path to the log file.</param>
    /// <returns>True if added successfully, false otherwise.</returns>
    Task<bool> AddLogAsync(string path);
}