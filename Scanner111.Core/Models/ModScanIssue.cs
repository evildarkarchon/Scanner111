namespace Scanner111.Core.Models;

/// <summary>
///     Represents different types of issues that can be found during mod scanning.
/// </summary>
public enum ModIssueType
{
    /// <summary>DDS texture dimensions are not divisible by 2</summary>
    TextureDimensions,
    
    /// <summary>Texture files have incorrect format (should be DDS)</summary>
    TextureFormat,
    
    /// <summary>Sound files have incorrect format (should be XWM or WAV)</summary>
    SoundFormat,
    
    /// <summary>Folder contains copies of XSE script files</summary>
    XseFiles,
    
    /// <summary>Folder contains loose precombine/previs files</summary>
    PrevisFiles,
    
    /// <summary>Folder contains custom animation file data</summary>
    AnimationData,
    
    /// <summary>BA2 archive has incorrect format</summary>
    Ba2Format,
    
    /// <summary>Documentation files that were cleaned up</summary>
    Cleanup
}

/// <summary>
///     Represents a specific issue found during mod file scanning.
/// </summary>
public sealed class ModScanIssue
{
    private ModScanIssue(ModIssueType issueType, string description, string? filePath = null, string? details = null)
    {
        IssueType = issueType;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        FilePath = filePath;
        Details = details;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Gets the type of issue detected.
    /// </summary>
    public ModIssueType IssueType { get; }

    /// <summary>
    ///     Gets the human-readable description of the issue.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the file path associated with the issue, if applicable.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    ///     Gets additional details about the issue.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    ///     Gets the timestamp when the issue was detected.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    ///     Creates a texture dimension issue.
    /// </summary>
    public static ModScanIssue CreateTextureDimensionIssue(string filePath, int width, int height)
    {
        return new ModScanIssue(
            ModIssueType.TextureDimensions,
            $"Texture dimensions ({width}x{height}) are not divisible by 2",
            filePath,
            $"Width: {width}, Height: {height}");
    }

    /// <summary>
    ///     Creates a file format issue.
    /// </summary>
    public static ModScanIssue CreateFormatIssue(ModIssueType issueType, string filePath, string expectedFormat, string actualFormat)
    {
        return new ModScanIssue(
            issueType,
            $"File has incorrect format: expected {expectedFormat}, found {actualFormat}",
            filePath,
            $"Expected: {expectedFormat}, Actual: {actualFormat}");
    }

    /// <summary>
    ///     Creates a generic issue with custom description.
    /// </summary>
    public static ModScanIssue CreateCustomIssue(ModIssueType issueType, string description, string? filePath = null, string? details = null)
    {
        return new ModScanIssue(issueType, description, filePath, details);
    }

    public override string ToString()
    {
        var result = $"[{IssueType}] {Description}";
        if (!string.IsNullOrEmpty(FilePath))
            result += $" - {FilePath}";
        return result;
    }
}