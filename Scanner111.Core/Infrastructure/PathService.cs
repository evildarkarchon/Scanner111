using System;
using System.IO;
using System.Linq;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Production implementation of IPathService with Windows path handling
/// </summary>
public class PathService : IPathService
{
    public string Combine(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;
            
        // Filter out empty strings and nulls
        var validPaths = paths.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        
        if (validPaths.Length == 0)
            return string.Empty;
            
        return Path.Combine(validPaths);
    }

    public string? GetDirectoryName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
            
        return Path.GetDirectoryName(path);
    }

    public string GetFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
            
        return Path.GetFileName(path);
    }

    public string GetFileNameWithoutExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
            
        return Path.GetFileNameWithoutExtension(path);
    }

    public string GetExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
            
        return Path.GetExtension(path);
    }

    public string GetFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
            
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            // Invalid path format
            return path;
        }
        catch (PathTooLongException)
        {
            // Path is too long
            return path;
        }
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        
        // Expand environment variables first
        path = Environment.ExpandEnvironmentVariables(path);
        
        // Handle forward slashes - convert to backslashes on Windows
        if (Path.DirectorySeparatorChar == '\\')
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);
        }
        
        // Remove any quotes that might have been added
        path = path.Trim('"', '\'');
        
        // Handle tilde expansion for user home directory
        if (path.StartsWith("~"))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = homeDirectory + path.Substring(1);
        }
        
        // Get the full path to resolve relative paths and normalize separators
        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            // If GetFullPath fails, just return the cleaned path
            // This can happen with invalid characters or other path issues
        }
        
        // Remove trailing directory separator unless it's a root path
        if (path.Length > 1 && path.EndsWith(Path.DirectorySeparatorChar.ToString()) && 
            !IsPathRoot(path))
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
        }
        
        return path;
    }

    public bool IsPathRooted(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
            
        return Path.IsPathRooted(path);
    }
    
    public char DirectorySeparatorChar => Path.DirectorySeparatorChar;
    
    private bool IsPathRoot(string path)
    {
        // Check if path is a root directory (e.g., "C:\" on Windows or "/" on Unix)
        if (string.IsNullOrEmpty(path))
            return false;
            
        // Windows root: "C:\" or "C:"
        if (Path.DirectorySeparatorChar == '\\')
        {
            return (path.Length == 2 && path[1] == ':') ||
                   (path.Length == 3 && path[1] == ':' && path[2] == '\\');
        }
        
        // Unix root: "/"
        return path == "/";
    }
}