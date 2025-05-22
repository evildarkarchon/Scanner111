using System.Threading.Tasks;

namespace Scanner111.Services;

/// <summary>
///     Interface for scanning mod INI files
/// </summary>
public interface IScanModInisService
{
    /// <summary>
    ///     Scans mod INI files for potential issues or incompatibilities.
    /// </summary>
    /// <returns>A detailed report of the mod INI file analysis.</returns>
    Task<string> ScanModInisAsync();
}