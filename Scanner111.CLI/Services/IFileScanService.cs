using Scanner111.Core.Models;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.CLI.Services;

/// <summary>
///     Represents a container for managing file scan data,
///     including the list of files to be scanned and a collection of copied files.
/// </summary>
public class FileScanData
{
    public List<string> FilesToScan { get; set; } = new();
    public HashSet<string> XseCopiedFiles { get; set; } = new();
}

/// <summary>
///     Defines a service for collecting files for scanning and managing crash log copying operations.
/// </summary>
public interface IFileScanService
{
    Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings);
}