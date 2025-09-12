namespace Scanner111.CLI.Services;

/// <summary>
/// Service for monitoring file changes with advanced features.
/// </summary>
public interface IFileWatcher : IDisposable
{
    /// <summary>
    /// Event raised when a monitored file changes.
    /// </summary>
    event EventHandler<FileChangedEventArgs>? FileChanged;
    
    /// <summary>
    /// Event triggered when an error occurs during file watching.
    /// </summary>
    event EventHandler<FileWatcherErrorEventArgs>? Error;
    
    /// <summary>
    /// Starts monitoring a file for changes.
    /// </summary>
    /// <param name="filePath">The file path to monitor.</param>
    /// <param name="filter">Optional filter pattern (e.g., "*.log").</param>
    void StartWatching(string filePath, string filter = "*.*");
    
    /// <summary>
    /// Starts watching multiple files.
    /// </summary>
    /// <param name="filePaths">The files to watch.</param>
    /// <param name="filter">Optional filter pattern (e.g., "*.log").</param>
    void StartWatchingMultiple(IEnumerable<string> filePaths, string filter = "*.*");
    
    /// <summary>
    /// Stops monitoring all files.
    /// </summary>
    void StopWatching();
    
    /// <summary>
    /// Stops watching a specific file.
    /// </summary>
    /// <param name="filePath">The file to stop watching.</param>
    void StopWatching(string filePath);
    
    /// <summary>
    /// Gets whether the watcher is currently active.
    /// </summary>
    bool IsWatching { get; }
    
    /// <summary>
    /// Gets the list of currently watched files.
    /// </summary>
    IReadOnlyList<string> WatchedFiles { get; }
    
    /// <summary>
    /// Gets the path being watched (for backward compatibility).
    /// </summary>
    string? WatchedPath { get; }
    
    /// <summary>
    /// Sets the debounce delay for file change events.
    /// </summary>
    /// <param name="delay">The delay in milliseconds.</param>
    void SetDebounceDelay(int delay);
    
    /// <summary>
    /// Forces a manual check of all watched files.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ForceCheckAsync();
}

/// <summary>
/// Event arguments for file change events.
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the full path of the changed file.
    /// </summary>
    public string FullPath { get; }
    
    /// <summary>
    /// Gets the type of change.
    /// </summary>
    public WatcherChangeTypes ChangeType { get; }
    
    /// <summary>
    /// Gets the timestamp of the change.
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="FileChangedEventArgs"/> class.
    /// </summary>
    public FileChangedEventArgs(string fullPath, WatcherChangeTypes changeType)
    {
        FullPath = fullPath;
        ChangeType = changeType;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for file watcher errors.
/// </summary>
public class FileWatcherErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the file path that caused the error.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the exception that occurred.
    /// </summary>
    public Exception Exception { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}