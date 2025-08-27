namespace Scanner111.Core.Models;

/// <summary>
///     Represents an important or recommended mod that should be installed.
///     Immutable record for thread-safety during concurrent analysis.
/// </summary>
public sealed record ImportantMod
{
    /// <summary>
    ///     Gets the identifier used to detect this mod in crash logs or plugin lists.
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Gets the display name of the mod.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    ///     Gets the recommendation message for this mod.
    /// </summary>
    public required string Recommendation { get; init; }

    /// <summary>
    ///     Gets whether this mod is installed (detected in plugins).
    /// </summary>
    public bool IsInstalled { get; init; }

    /// <summary>
    ///     Gets whether this mod has GPU-specific requirements or warnings.
    /// </summary>
    public bool HasGpuRequirement { get; init; }

    /// <summary>
    ///     Gets the GPU type this mod is designed for (nvidia, amd), if any.
    /// </summary>
    public string? RequiredGpuType { get; init; }

    /// <summary>
    ///     Gets the detected GPU type from the system.
    /// </summary>
    public string? DetectedGpuType { get; init; }

    /// <summary>
    ///     Gets whether there's a GPU compatibility issue.
    /// </summary>
    public bool HasGpuCompatibilityIssue { get; init; }

    /// <summary>
    ///     Gets the category of this important mod (e.g., "Core", "Performance", "Stability").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    ///     Creates an important mod from detection data.
    /// </summary>
    public static ImportantMod Create(
        string modId,
        string displayName,
        string recommendation,
        bool isInstalled = false,
        string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendation);

        return new ImportantMod
        {
            ModId = modId,
            DisplayName = displayName,
            Recommendation = recommendation,
            IsInstalled = isInstalled,
            Category = category
        };
    }

    /// <summary>
    ///     Creates an important mod with GPU compatibility checking.
    /// </summary>
    public static ImportantMod CreateWithGpuCheck(
        string modId,
        string displayName,
        string recommendation,
        bool isInstalled,
        string? requiredGpuType,
        string? detectedGpuType,
        string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendation);

        var hasGpuRequirement = !string.IsNullOrWhiteSpace(requiredGpuType);
        var hasCompatibilityIssue = hasGpuRequirement &&
                                   !string.IsNullOrWhiteSpace(detectedGpuType) &&
                                   !string.Equals(requiredGpuType, detectedGpuType, StringComparison.OrdinalIgnoreCase);

        return new ImportantMod
        {
            ModId = modId,
            DisplayName = displayName,
            Recommendation = recommendation,
            IsInstalled = isInstalled,
            HasGpuRequirement = hasGpuRequirement,
            RequiredGpuType = requiredGpuType,
            DetectedGpuType = detectedGpuType,
            HasGpuCompatibilityIssue = hasCompatibilityIssue,
            Category = category
        };
    }

    /// <summary>
    ///     Gets the status of this important mod for reporting.
    /// </summary>
    public ModStatus GetStatus()
    {
        if (IsInstalled && HasGpuCompatibilityIssue)
            return ModStatus.InstalledWithGpuIssue;
        
        if (IsInstalled)
            return ModStatus.Installed;
            
        if (HasGpuRequirement && !string.IsNullOrWhiteSpace(DetectedGpuType) && 
            !string.Equals(RequiredGpuType, DetectedGpuType, StringComparison.OrdinalIgnoreCase))
            return ModStatus.NotNeededForGpu;
            
        return ModStatus.NotInstalled;
    }
}

/// <summary>
///     Status of an important mod for reporting purposes.
/// </summary>
public enum ModStatus
{
    /// <summary>
    ///     Mod is installed and working correctly.
    /// </summary>
    Installed,
    
    /// <summary>
    ///     Mod is not installed but should be.
    /// </summary>
    NotInstalled,
    
    /// <summary>
    ///     Mod is installed but has GPU compatibility issues.
    /// </summary>
    InstalledWithGpuIssue,
    
    /// <summary>
    ///     Mod is not needed due to GPU type mismatch.
    /// </summary>
    NotNeededForGpu
}