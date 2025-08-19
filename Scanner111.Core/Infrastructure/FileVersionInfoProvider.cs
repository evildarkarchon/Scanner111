using System;
using System.Diagnostics;
using System.IO;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Production implementation of IFileVersionInfoProvider
/// </summary>
public class FileVersionInfoProvider : IFileVersionInfoProvider
{
    public FileVersionInfo GetVersionInfo(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));
            
        if (!File.Exists(fileName))
            throw new FileNotFoundException($"File not found: {fileName}", fileName);
            
        return FileVersionInfo.GetVersionInfo(fileName);
    }

    public bool TryGetVersionInfo(string fileName, out FileVersionInfo? versionInfo)
    {
        versionInfo = null;
        
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
            
        if (!File.Exists(fileName))
            return false;
            
        try
        {
            versionInfo = FileVersionInfo.GetVersionInfo(fileName);
            return true;
        }
        catch (Exception)
        {
            // Some files may not have version information or may be locked
            return false;
        }
    }
}