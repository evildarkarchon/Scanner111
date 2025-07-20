using System.Collections.Generic;
using System.Threading.Tasks;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Services;

/// <summary>
/// Data container for file scan results
/// </summary>
public class FileScanData
{
    public List<string> FilesToScan { get; set; } = new();
    public HashSet<string> XseCopiedFiles { get; set; } = new();
}

/// <summary>
/// Service for collecting files to scan and handling XSE crash log copying
/// </summary>
public interface IFileScanService
{
    Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings);
}