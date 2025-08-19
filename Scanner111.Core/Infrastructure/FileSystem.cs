using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Production implementation of IFileSystem using System.IO
/// </summary>
public class FileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
            
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
            
        return Directory.Exists(path);
    }

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!DirectoryExists(path))
            return Array.Empty<string>();
            
        try
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetDirectories(string path)
    {
        if (!DirectoryExists(path))
            return Array.Empty<string>();
            
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public Stream OpenWrite(string path)
    {
        // Ensure directory exists before creating file
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        return File.Create(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteFile(string path)
    {
        if (FileExists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        if (DirectoryExists(path))
        {
            Directory.Delete(path, recursive);
        }
    }

    public void CopyFile(string source, string destination, bool overwrite = false)
    {
        // Ensure destination directory exists
        var destDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }
        
        File.Copy(source, destination, overwrite);
    }

    public void MoveFile(string source, string destination)
    {
        // Ensure destination directory exists
        var destDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }
        
        File.Move(source, destination);
    }

    public DateTime GetLastWriteTime(string path)
    {
        if (FileExists(path))
            return File.GetLastWriteTime(path);
        if (DirectoryExists(path))
            return Directory.GetLastWriteTime(path);
            
        return DateTime.MinValue;
    }

    public long GetFileSize(string path)
    {
        if (!FileExists(path))
            return 0;
            
        var fileInfo = new FileInfo(path);
        return fileInfo.Length;
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public void WriteAllText(string path, string content)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(path, content);
    }

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }
}