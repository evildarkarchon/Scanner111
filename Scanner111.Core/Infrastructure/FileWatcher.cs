using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Real implementation of file watcher using FileSystemWatcher
/// </summary>
public class FileWatcher : IFileWatcher
{
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;
    
    public FileWatcher(string path, string filter)
    {
        _watcher = new FileSystemWatcher(path, filter);
        
        // Forward events
        _watcher.Changed += (sender, e) => Changed?.Invoke(sender, e);
        _watcher.Error += (sender, e) => Error?.Invoke(sender, e);
    }
    
    public string Path
    {
        get => _watcher.Path;
        set => _watcher.Path = value;
    }
    
    public string Filter
    {
        get => _watcher.Filter;
        set => _watcher.Filter = value;
    }
    
    public NotifyFilters NotifyFilter
    {
        get => _watcher.NotifyFilter;
        set => _watcher.NotifyFilter = value;
    }
    
    public bool EnableRaisingEvents
    {
        get => _watcher.EnableRaisingEvents;
        set => _watcher.EnableRaisingEvents = value;
    }
    
    public event FileSystemEventHandler? Changed;
    public event ErrorEventHandler? Error;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _watcher?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Factory for creating real file watchers
/// </summary>
public class FileWatcherFactory : IFileWatcherFactory
{
    public IFileWatcher CreateWatcher(string path, string filter)
    {
        return new FileWatcher(path, filter);
    }
}