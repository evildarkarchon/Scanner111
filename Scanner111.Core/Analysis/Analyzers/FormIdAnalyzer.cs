using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Data;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes crash logs to extract and identify FormIDs that may be related to crashes.
/// Thread-safe with async database lookups for FormID information.
/// </summary>
public sealed partial class FormIdAnalyzer : AnalyzerBase
{
    private readonly IFormIdDatabase? _formIdDatabase;
    private readonly Lazy<Regex> _formIdPattern;
    private readonly bool _showFormIdValues;
    private readonly string _crashGenName;
    
    public FormIdAnalyzer(
        ILogger<FormIdAnalyzer> logger,
        IFormIdDatabase? formIdDatabase = null,
        bool showFormIdValues = true,
        string? crashGenName = null) 
        : base(logger)
    {
        _formIdDatabase = formIdDatabase;
        _showFormIdValues = showFormIdValues;
        _crashGenName = crashGenName ?? "Scanner111";
        _formIdPattern = new Lazy<Regex>(() => FormIdRegex());
    }
    
    /// <inheritdoc />
    public override string Name => "FormIdAnalyzer";
    
    /// <inheritdoc />
    public override string DisplayName => "FormID Analysis";
    
    /// <inheritdoc />
    public override int Priority => 60;
    
    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromSeconds(45); // Allow more time for database lookups
    
    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting FormID analysis for {Path}", context.InputPath);
        
        // Get call stack segment from shared data
        if (!context.TryGetSharedData<List<string>>("CallStackSegment", out var callStackSegment) || 
            callStackSegment == null || callStackSegment.Count == 0)
        {
            LogDebug("No call stack segment found in context");
            return CreateSuccessResult(
                "FormID Analysis",
                "* COULDN'T FIND ANY FORM ID SUSPECTS *\n\n",
                Priority);
        }
        
        // Get plugin list from shared data
        if (!context.TryGetSharedData<Dictionary<string, string>>("CrashLogPlugins", out var crashLogPlugins) || 
            crashLogPlugins == null)
        {
            LogWarning("No plugin list found in context, using empty plugin list");
            crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        
        // Extract FormIDs from the call stack
        var formIds = ExtractFormIds(callStackSegment);
        if (formIds.Count == 0)
        {
            LogDebug("No FormIDs found in call stack");
            return CreateSuccessResult(
                "FormID Analysis",
                "* COULDN'T FIND ANY FORM ID SUSPECTS *\n\n",
                Priority);
        }
        
        LogInformation("Found {Count} FormID suspects", formIds.Count);
        
        // Match FormIDs with plugins and perform database lookups
        var reportFragment = await CreateFormIdReportAsync(formIds, crashLogPlugins, cancellationToken)
            .ConfigureAwait(false);
        
        var result = AnalysisResult.CreateSuccess(Name, reportFragment);
        result.AddMetadata("FormIdCount", formIds.Count.ToString());
        
        return result;
    }
    
    /// <summary>
    /// Extracts FormIDs from the call stack segment.
    /// </summary>
    private List<string> ExtractFormIds(List<string> callStackSegment)
    {
        var formIds = new List<string>();
        var regex = _formIdPattern.Value;
        
        foreach (var line in callStackSegment)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                var formId = match.Groups[1].Value.ToUpperInvariant();
                // Skip if it starts with FF (plugin limit)
                if (!formId.StartsWith("FF", StringComparison.OrdinalIgnoreCase))
                {
                    formIds.Add($"Form ID: {formId}");
                }
            }
        }
        
        return formIds;
    }
    
    /// <summary>
    /// Creates a report fragment for FormID analysis with optional database lookups.
    /// </summary>
    private async Task<ReportFragment> CreateFormIdReportAsync(
        List<string> formIdMatches,
        Dictionary<string, string> crashLogPlugins,
        CancellationToken cancellationToken)
    {
        // Group and count FormIDs
        var formIdCounts = formIdMatches
            .GroupBy(f => f)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var reportLines = new StringBuilder();
        
        // Prepare lookup tasks for database queries
        var lookupTasks = new List<FormIdLookupTask>();
        
        foreach (var (formIdFull, count) in formIdCounts)
        {
            var parts = formIdFull.Split(": ", 2);
            if (parts.Length < 2)
                continue;
            
            var formIdValue = parts[1];
            var formIdPrefix = formIdValue.Substring(0, 2);
            var formIdSuffix = formIdValue.Substring(2);
            
            // Find matching plugin
            var matchingPlugin = crashLogPlugins
                .FirstOrDefault(p => string.Equals(p.Value, formIdPrefix, StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrEmpty(matchingPlugin.Key))
            {
                lookupTasks.Add(new FormIdLookupTask
                {
                    FormIdFull = formIdFull,
                    FormIdSuffix = formIdSuffix,
                    Plugin = matchingPlugin.Key,
                    Count = count
                });
            }
            else
            {
                // No matching plugin found
                reportLines.AppendLine($"- {formIdFull} | [Unknown Plugin {formIdPrefix}] | {count}");
            }
        }
        
        // Perform database lookups if enabled and available
        if (_showFormIdValues && _formIdDatabase?.IsAvailable == true && lookupTasks.Count > 0)
        {
            LogDebug("Performing database lookups for {Count} FormIDs", lookupTasks.Count);
            await PerformDatabaseLookupsAsync(lookupTasks, reportLines, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // No database lookups - just format the basic information
            foreach (var task in lookupTasks)
            {
                reportLines.AppendLine($"- {task.FormIdFull} | [{task.Plugin}] | {task.Count}");
            }
        }
        
        // Add footer information
        reportLines.AppendLine();
        reportLines.AppendLine("[Last number counts how many times each Form ID shows up in the crash log.]");
        reportLines.AppendLine($"These Form IDs were caught by {_crashGenName} and some of them might be related to this crash.");
        reportLines.AppendLine("You can try searching any listed Form IDs in xEdit and see if they lead to relevant records.");
        reportLines.AppendLine();
        
        return ReportFragment.CreateSection("FormID Suspects", reportLines.ToString(), Priority);
    }
    
    /// <summary>
    /// Performs parallel database lookups for FormID information.
    /// </summary>
    private async Task PerformDatabaseLookupsAsync(
        List<FormIdLookupTask> lookupTasks,
        StringBuilder reportLines,
        CancellationToken cancellationToken)
    {
        if (_formIdDatabase == null)
            return;
        
        try
        {
            // Prepare queries for batch lookup
            var queries = lookupTasks
                .Select(t => (formId: t.FormIdSuffix, plugin: t.Plugin))
                .ToArray();
            
            // Perform batch lookup for better performance
            var results = await _formIdDatabase.GetEntriesAsync(queries, cancellationToken)
                .ConfigureAwait(false);
            
            // Format results
            for (int i = 0; i < lookupTasks.Count; i++)
            {
                var task = lookupTasks[i];
                var result = i < results.Length ? results[i] : null;
                
                if (!string.IsNullOrEmpty(result))
                {
                    reportLines.AppendLine($"- {task.FormIdFull} | [{task.Plugin}] | {result} | {task.Count}");
                }
                else
                {
                    reportLines.AppendLine($"- {task.FormIdFull} | [{task.Plugin}] | {task.Count}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogWarning("Database lookups cancelled, falling back to basic formatting");
            // Fallback to basic formatting
            foreach (var task in lookupTasks)
            {
                reportLines.AppendLine($"- {task.FormIdFull} | [{task.Plugin}] | {task.Count}");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error performing database lookups, falling back to basic formatting");
            // Fallback to basic formatting
            foreach (var task in lookupTasks)
            {
                reportLines.AppendLine($"- {task.FormIdFull} | [{task.Plugin}] | {task.Count}");
            }
        }
    }
    
    /// <inheritdoc />
    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        // Initialize database if available
        if (_formIdDatabase != null && !_formIdDatabase.IsAvailable)
        {
            try
            {
                await _formIdDatabase.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to initialize FormID database");
            }
        }
        
        return await base.CanAnalyzeAsync(context).ConfigureAwait(false);
    }
    
    // Compiled regex for better performance
    [GeneratedRegex(@"^\s*Form ID:\s*0x([0-9A-Fa-f]{8})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FormIdRegex();
    
    /// <summary>
    /// Helper class for FormID lookup tasks.
    /// </summary>
    private sealed class FormIdLookupTask
    {
        public required string FormIdFull { get; init; }
        public required string FormIdSuffix { get; init; }
        public required string Plugin { get; init; }
        public required int Count { get; init; }
    }
}