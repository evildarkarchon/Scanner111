using System.Threading.Tasks;

namespace Scanner111.Services;

/// <summary>
///     Interface for checking XSE plugins and Address Library
/// </summary>
public interface ICheckXsePluginsService
{
    /// <summary>
    ///     Checks for XSE plugin and Address Library issues.
    /// </summary>
    /// <returns>A detailed report of XSE plugin and Address Library analysis.</returns>
    Task<string> CheckXsePluginsAsync();
}