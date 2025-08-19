namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for file system watching operations
/// </summary>
public interface IFileWatcher : IDisposable
{
    /// <summary>
    /// Gets or sets the path to monitor
    /// </summary>
    string Path { get; set; }
    
    /// <summary>
    /// Gets or sets the filter for files to watch
    /// </summary>
    string Filter { get; set; }
    
    /// <summary>
    /// Gets or sets the types of changes to monitor
    /// </summary>
    NotifyFilters NotifyFilter { get; set; }
    
    /// <summary>
    /// Gets or sets whether the watcher is enabled
    /// </summary>
    bool EnableRaisingEvents { get; set; }
    
    /// <summary>
    /// Occurs when a file or directory in the specified Path is changed
    /// </summary>
    event FileSystemEventHandler? Changed;
    
    /// <summary>
    /// Occurs when the watcher is unable to continue monitoring
    /// </summary>
    event ErrorEventHandler? Error;
}

/// <summary>
/// Factory for creating file watchers
/// </summary>
public interface IFileWatcherFactory
{
    /// <summary>
    /// Creates a new file watcher for the specified path and filter
    /// </summary>
    IFileWatcher CreateWatcher(string path, string filter);
}