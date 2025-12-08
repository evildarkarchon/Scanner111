namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents a FormID record from the database.
/// </summary>
public record FormIdRecord
{
    /// <summary>
    /// Gets the FormID (e.g., "00012345").
    /// </summary>
    public string FormID { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the record associated with the FormID.
    /// </summary>
    public string RecordName { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the plugin file name that this FormID belongs to.
    /// </summary>
    public string Plugin { get; init; } = string.Empty;
}
