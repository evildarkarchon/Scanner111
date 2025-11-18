namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents information about a DLL module loaded by the game at the time of the crash.
/// This includes game DLLs, script extenders, and third-party tools.
/// </summary>
public record ModuleInfo
{
    /// <summary>
    /// Gets the name of the module (e.g., "f4se_1_10_163.dll").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the version of the module, if available.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the file system path to the module, if available.
    /// </summary>
    public string? Path { get; init; }
}
