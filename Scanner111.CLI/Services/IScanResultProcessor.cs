using System.Collections.Generic;
using System.Threading.Tasks;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for processing scan results and generating reports
/// </summary>
public interface IScanResultProcessor
{
    Task ProcessScanResultAsync(ScanResult result, CliScanOptions options, IReportWriter reportWriter, HashSet<string> xseCopiedFiles, ApplicationSettings settings);
}