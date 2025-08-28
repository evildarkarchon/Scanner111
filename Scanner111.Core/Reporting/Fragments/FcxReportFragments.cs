using System.Text;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Reporting.Fragments;

/// <summary>
///     Specialized report fragment builders for FCX mode results.
///     Provides structured, reusable fragments with metadata enrichment.
/// </summary>
public static class FcxReportFragments
{
    /// <summary>
    ///     Creates a comprehensive FCX status fragment with all check results.
    /// </summary>
    public static ReportFragment CreateStatusFragment(
        bool fcxEnabled,
        string? mainFilesResult = null,
        string? modFilesResult = null,
        int order = 10)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Mode Status")
            .WithType(fcxEnabled ? FragmentType.Info : FragmentType.Warning)
            .WithOrder(order)
            .WithMetadata("FcxEnabled", fcxEnabled.ToString());

        if (fcxEnabled)
        {
            builder.AppendLine("* NOTICE: FCX MODE IS ENABLED. Scanner111 MUST BE RUN BY THE ORIGINAL USER FOR CORRECT DETECTION *")
                   .AppendLine()
                   .AppendLine("[ To disable mod & game files detection, disable FCX Mode in Scanner111 Settings.yaml ]")
                   .AppendLine();

            if (!string.IsNullOrWhiteSpace(mainFilesResult))
            {
                builder.WithMetadata("MainFilesChecked", "true");
                builder.Append(mainFilesResult);
            }

            if (!string.IsNullOrWhiteSpace(modFilesResult))
            {
                builder.WithMetadata("ModFilesChecked", "true");
                builder.Append(modFilesResult);
            }
        }
        else
        {
            builder.AppendLine("* NOTICE: FCX MODE IS DISABLED. YOU CAN ENABLE IT TO DETECT PROBLEMS IN YOUR MOD & GAME FILES *")
                   .AppendLine()
                   .AppendLine("[ FCX Mode can be enabled in Scanner111 Settings.yaml located in your Scanner111 folder. ]")
                   .AppendLine();
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a fragment specifically for file integrity check results.
    /// </summary>
    public static ReportFragment CreateFileIntegrityFragment(
        string checkResult,
        bool hasErrors,
        int order = 20)
    {
        var type = hasErrors ? FragmentType.Error : FragmentType.Info;
        var title = hasErrors ? "File Integrity Issues Detected" : "File Integrity Check Passed";

        return ReportFragmentBuilder.Create()
            .WithTitle(title)
            .WithType(type)
            .WithOrder(order)
            .WithMetadata("HasIntegrityIssues", hasErrors.ToString())
            .Append(checkResult)
            .Build();
    }

    /// <summary>
    ///     Creates a fragment for mod file scan results with detailed findings.
    /// </summary>
    public static ReportFragment CreateModFilesFragment(
        string scanResult,
        int modCount,
        int issueCount,
        int order = 30)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Mod Files Analysis")
            .WithOrder(order)
            .WithMetadata("ModCount", modCount.ToString())
            .WithMetadata("IssueCount", issueCount.ToString());

        if (issueCount > 0)
        {
            builder.WithType(FragmentType.Warning)
                   .AppendWarning($"Found {issueCount} potential issues in {modCount} mods")
                   .AppendLine();
        }
        else
        {
            builder.WithType(FragmentType.Info)
                   .AppendSuccess($"All {modCount} mods appear to be properly configured")
                   .AppendLine();
        }

        builder.Append(scanResult);
        return builder.Build();
    }

    /// <summary>
    ///     Creates a consolidated error fragment for FCX failures.
    /// </summary>
    public static ReportFragment CreateErrorFragment(
        string errorMessage,
        string? stackTrace = null,
        string? suggestion = null,
        int order = 5)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Mode Check Failed")
            .WithType(FragmentType.Error)
            .WithOrder(order)
            .WithMetadata("HasError", "true")
            .AppendError($"FCX checks encountered an error: {errorMessage}");

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            builder.AppendLine()
                   .AppendLine("Technical Details:")
                   .AppendLine("```")
                   .AppendLine(stackTrace)
                   .AppendLine("```");
        }

        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            builder.AppendLine()
                   .AppendSolution(suggestion);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a summary fragment with key FCX metrics.
    /// </summary>
    public static ReportFragment CreateSummaryFragment(
        FcxFileCheckResult result,
        TimeSpan duration,
        int order = 15)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Mode Summary")
            .WithType(result.Success ? FragmentType.Info : FragmentType.Warning)
            .WithOrder(order)
            .WithMetadata("Duration", duration.TotalSeconds.ToString("F2"))
            .WithMetadata("Success", result.Success.ToString());

        if (result.Success)
        {
            builder.AppendSuccess("FCX checks completed successfully")
                   .AppendFormatted(" in {0:F2} seconds", duration.TotalSeconds)
                   .AppendLine();

            // Add summary statistics
            var stats = ExtractStatistics(result);
            if (stats.Any())
            {
                builder.AppendLine()
                       .AppendLine("Check Results:");
                foreach (var (key, value) in stats)
                {
                    builder.AppendLine($"  • {key}: {value}");
                }
            }
        }
        else
        {
            builder.AppendWarning("FCX checks did not complete successfully")
                   .AppendLine();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                builder.AppendError(result.ErrorMessage);
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a detailed fragment with full FCX check results.
    /// </summary>
    public static ReportFragment CreateDetailedFragment(
        FcxFileCheckResult result,
        Dictionary<string, string>? additionalInfo = null,
        int order = 25)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Mode Detailed Results")
            .WithType(FragmentType.Section)
            .WithOrder(order)
            .WithMetadata("Timestamp", result.CompletedAt.ToString("O"));

        // Main files section
        if (!string.IsNullOrWhiteSpace(result.MainFilesResult))
        {
            builder.AppendLine()
                   .AppendLine("**Main Game Files:**")
                   .Append(result.MainFilesResult)
                   .AppendLine();
        }

        // Mod files section
        if (!string.IsNullOrWhiteSpace(result.ModFilesResult))
        {
            builder.AppendLine("**Mod Files:**")
                   .Append(result.ModFilesResult)
                   .AppendLine();
        }

        // Additional information
        if (additionalInfo?.Count > 0)
        {
            builder.AppendLine("**Additional Information:**");
            foreach (var (key, value) in additionalInfo)
            {
                builder.AppendLine($"  • {key}: {value}");
            }
        }

        // Metadata
        if (result.Metadata.Count > 0)
        {
            foreach (var (key, value) in result.Metadata)
            {
                builder.WithMetadata(key, value);
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Creates a fragment for FCX mode configuration instructions.
    /// </summary>
    public static ReportFragment CreateConfigurationFragment(
        bool fcxEnabled,
        string settingsPath,
        int order = 100)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Mode Configuration")
            .WithType(FragmentType.Info)
            .WithOrder(order)
            .WithMetadata("CurrentState", fcxEnabled ? "Enabled" : "Disabled");

        builder.AppendLine($"FCX Mode is currently: **{(fcxEnabled ? "ENABLED" : "DISABLED")}**")
               .AppendLine()
               .AppendLine("To change FCX Mode settings:")
               .AppendLine($"  1. Open the settings file: {settingsPath}")
               .AppendLine($"  2. Find the 'FcxMode' setting")
               .AppendLine($"  3. Set to 'true' to enable or 'false' to disable")
               .AppendLine($"  4. Save the file and restart Scanner111")
               .AppendLine()
               .AppendNotice("FCX Mode enables extended file integrity checking for game and mod files");

        return builder.Build();
    }

    /// <summary>
    ///     Creates a performance metrics fragment for FCX operations.
    /// </summary>
    public static ReportFragment CreatePerformanceFragment(
        Dictionary<string, TimeSpan> operationTimings,
        long filesChecked,
        long totalSizeBytes,
        int order = 90)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("FCX Performance Metrics")
            .WithType(FragmentType.Info)
            .WithOrder(order)
            .WithVisibility(FragmentVisibility.Verbose)
            .WithMetadata("FilesChecked", filesChecked.ToString())
            .WithMetadata("TotalSizeBytes", totalSizeBytes.ToString());

        builder.AppendLine("Performance Statistics:");
        
        // Operation timings
        if (operationTimings.Count > 0)
        {
            builder.AppendLine()
                   .AppendLine("Operation Timings:");
            foreach (var (operation, duration) in operationTimings.OrderBy(x => x.Value))
            {
                builder.AppendLine($"  • {operation}: {duration.TotalMilliseconds:F2}ms");
            }
        }

        // File statistics
        builder.AppendLine()
               .AppendLine("File Statistics:")
               .AppendLine($"  • Files Checked: {filesChecked:N0}")
               .AppendLine($"  • Total Size: {FormatBytes(totalSizeBytes)}")
               .AppendLine($"  • Average Size: {FormatBytes(totalSizeBytes / Math.Max(1, filesChecked))}");

        if (operationTimings.TryGetValue("Total", out var totalTime) && filesChecked > 0)
        {
            var filesPerSecond = filesChecked / Math.Max(0.001, totalTime.TotalSeconds);
            builder.AppendLine($"  • Processing Speed: {filesPerSecond:F2} files/sec");
        }

        return builder.Build();
    }

    /// <summary>
    ///     Combines multiple FCX fragments with proper hierarchy.
    /// </summary>
    public static ReportFragment? CombineFragments(
        string title,
        IEnumerable<ReportFragment?> fragments,
        int order = 10)
    {
        var nonEmptyFragments = fragments
            .Where(f => f != null && f.HasContent())
            .OrderBy(f => f!.Order)
            .ToList();

        if (!nonEmptyFragments.Any())
            return null;

        // Determine overall type based on fragment priorities
        var overallType = DetermineOverallType(nonEmptyFragments!);

        var combinedBuilder = ReportFragmentBuilder.Create()
            .WithTitle(title)
            .WithType(overallType)
            .WithOrder(order);

        // Add metadata from all fragments
        var combinedMetadata = new Dictionary<string, string>();
        foreach (var fragment in nonEmptyFragments!)
        {
            if (fragment.Metadata != null)
            {
                foreach (var (key, value) in fragment.Metadata)
                {
                    combinedMetadata[key] = value;
                }
            }
        }

        foreach (var (key, value) in combinedMetadata)
        {
            combinedBuilder.WithMetadata(key, value);
        }

        // Build hierarchical structure
        var combined = combinedBuilder.Build();
        return ReportFragment.CreateWithChildren(title, nonEmptyFragments!, order);
    }

    private static Dictionary<string, string> ExtractStatistics(FcxFileCheckResult result)
    {
        var stats = new Dictionary<string, string>();

        if (result.MainFilesResult.Contains("✔️"))
            stats["Main Files"] = "Passed";
        else if (result.MainFilesResult.Contains("❌"))
            stats["Main Files"] = "Failed";

        if (result.ModFilesResult.Contains("✔️"))
            stats["Mod Files"] = "Passed";
        else if (result.ModFilesResult.Contains("❌"))
            stats["Mod Files"] = "Failed";

        return stats;
    }

    private static FragmentType DetermineOverallType(IEnumerable<ReportFragment> fragments)
    {
        var types = fragments.Select(f => f.Type).ToList();
        
        if (types.Contains(FragmentType.Error))
            return FragmentType.Error;
        if (types.Contains(FragmentType.Warning))
            return FragmentType.Warning;
        if (types.Contains(FragmentType.Info))
            return FragmentType.Info;
        
        return FragmentType.Section;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:F2} {sizes[order]}";
    }
}