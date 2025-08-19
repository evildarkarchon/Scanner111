using System;

namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for environment and system path operations
/// </summary>
public interface IEnvironmentPathProvider
{
    /// <summary>
    /// Gets the path to the system special folder identified by the specified enumeration
    /// </summary>
    string GetFolderPath(Environment.SpecialFolder folder);

    /// <summary>
    /// Retrieves the value of an environment variable
    /// </summary>
    string? GetEnvironmentVariable(string variable);

    /// <summary>
    /// Gets or sets the fully qualified path of the current working directory
    /// </summary>
    string CurrentDirectory { get; }

    /// <summary>
    /// Gets the path of the system's temporary folder
    /// </summary>
    string TempPath { get; }

    /// <summary>
    /// Gets the user name of the person who is currently logged on to the operating system
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// Gets the NetBIOS name of this local computer
    /// </summary>
    string MachineName { get; }
}