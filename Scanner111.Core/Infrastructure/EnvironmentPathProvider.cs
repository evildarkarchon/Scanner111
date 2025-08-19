using System;
using System.IO;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Production implementation of IEnvironmentPathProvider
/// </summary>
public class EnvironmentPathProvider : IEnvironmentPathProvider
{
    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return Environment.GetFolderPath(folder);
    }

    public string? GetEnvironmentVariable(string variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return null;
            
        return Environment.GetEnvironmentVariable(variable);
    }

    public string CurrentDirectory => Environment.CurrentDirectory;

    public string TempPath => Path.GetTempPath();

    public string UserName => Environment.UserName;

    public string MachineName => Environment.MachineName;
}