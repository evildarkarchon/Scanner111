using System.Threading.Tasks;

namespace Scanner111.Services;

/// <summary>
///     Interface for scanning Wrye Bash reports
/// </summary>
public interface IScanWryeCheckService
{
    /// <summary>
    ///     Analyzes Wrye Bash reports for potential issues.
    /// </summary>
    /// <returns>A detailed analysis report from Wrye Bash data.</returns>
    Task<string> ScanWryeCheckAsync();
}