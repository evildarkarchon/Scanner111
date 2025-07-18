using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Models;

/// <summary>
/// Result of analyzing a crash log
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Path to the original crash log file
    /// </summary>
    public required string LogPath { get; init; }
    
    /// <summary>
    /// Status of the scan
    /// </summary>
    public ScanStatus Status { get; set; }
    
    /// <summary>
    /// The parsed crash log
    /// </summary>
    public CrashLog? CrashLog { get; set; }
    
    /// <summary>
    /// Analysis results from each analyzer
    /// </summary>
    public List<AnalysisResult> AnalysisResults { get; init; } = new();
    
    /// <summary>
    /// Generated report lines
    /// </summary>
    public List<string> Report { get; init; } = new();
    
    /// <summary>
    /// Errors encountered during the scan
    /// </summary>
    public List<string> ErrorMessages { get; init; } = new();
    
    /// <summary>
    /// True if the scan failed due to an error
    /// </summary>
    public bool Failed => Status == ScanStatus.Failed;
    
    /// <summary>
    /// True if there were any errors
    /// </summary>
    public bool HasErrors => ErrorMessages.Any();
    
    /// <summary>
    /// Time taken to process
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Statistics about the scan operation
    /// </summary>
    public ScanStatistics Statistics { get; init; } = new();
    
    /// <summary>
    /// Full report text as a single string
    /// </summary>
    public string ReportText => Report.Any() ? string.Join("", Report) : (CrashLog != null ? GenerateFormattedReport() : string.Empty);
    
    /// <summary>
    /// Whether this log was copied from XSE (script extender) directory
    /// </summary>
    public bool WasCopiedFromXSE { get; set; }
    
    /// <summary>
    /// Suggested output path for the AUTOSCAN report
    /// </summary>
    public string OutputPath
    {
        get
        {
            // If log was copied from XSE directory, write report alongside the copied log
            // Otherwise, write report alongside the original source log
            return Path.ChangeExtension(LogPath, null) + "-AUTOSCAN.md";
        }
    }
    
    /// <summary>
    /// Add an analysis result to this scan result
    /// </summary>
    public void AddAnalysisResult(AnalysisResult result)
    {
        AnalysisResults.Add(result);
        // Don't add to Report here - we'll generate the formatted report later
    }
    
    /// <summary>
    /// Add an error message
    /// </summary>
    public void AddError(string error)
    {
        ErrorMessages.Add(error);
    }
    
    /// <summary>
    /// Generate the formatted report matching Python CLASSIC output
    /// </summary>
    private string GenerateFormattedReport()
    {
        var report = new List<string>();
        
        // Header section
        var logFileName = Path.GetFileName(LogPath);
        report.Add($"{logFileName} -> AUTOSCAN REPORT GENERATED BY Scanner 111 v1.0.0 \n");
        report.Add("# FOR BEST VIEWING EXPERIENCE OPEN THIS FILE IN NOTEPAD++ OR SIMILAR # \n");
        report.Add("# PLEASE READ EVERYTHING CAREFULLY AND BEWARE OF FALSE POSITIVES # \n");
        report.Add("====================================================\n");
        report.Add("\n");
        
        // Main error and version info
        if (CrashLog != null)
        {
            report.Add($"Main Error: {CrashLog.MainError}\n");
            if (!string.IsNullOrEmpty(CrashLog.CrashGenVersion))
            {
                report.Add($"Detected Buffout 4 Version: {CrashLog.CrashGenVersion} \n");
                // TODO: Add version comparison logic
                report.Add("* You have the latest version of Buffout 4! *\n");
            }
            report.Add("\n");
        }
        
        // Suspects section
        var suspectResult = AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Suspect Scanner");
        if (suspectResult != null && suspectResult.HasFindings)
        {
            report.Add("====================================================\n");
            report.Add("CHECKING IF LOG MATCHES ANY KNOWN CRASH SUSPECTS...\n");
            report.Add("====================================================\n");
            report.AddRange(suspectResult.ReportLines);
            report.Add("* FOR DETAILED DESCRIPTIONS AND POSSIBLE SOLUTIONS TO ANY ABOVE DETECTED CRASH SUSPECTS *\n");
            report.Add("* SEE: https://docs.google.com/document/d/17FzeIMJ256xE85XdjoPvv_Zi3C5uHeSTQh6wOZugs4c *\n");
            report.Add("\n");
        }
        
        // Settings section  
        var settingsResult = AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Settings Scanner");
        if (settingsResult != null && settingsResult.HasFindings)
        {
            report.Add("====================================================\n");
            report.Add("CHECKING IF NECESSARY FILES/SETTINGS ARE CORRECT...\n");
            report.Add("====================================================\n");
            // TODO: Add FCX mode notice
            report.Add("* NOTICE: FCX MODE IS DISABLED. YOU CAN ENABLE IT TO DETECT PROBLEMS IN YOUR MOD & GAME FILES * \n");
            report.Add("[ FCX Mode can be enabled in the Scanner 111 application settings. ] \n");
            report.Add("\n");
            report.AddRange(settingsResult.ReportLines);
        }
        
        // Additional sections
        report.Add("====================================================\n");
        report.Add("CHECKING FOR MODS THAT CAN CAUSE FREQUENT CRASHES...\n");
        report.Add("====================================================\n");
        
        report.Add("====================================================\n");
        report.Add("CHECKING FOR MODS THAT CONFLICT WITH OTHER MODS...\n");
        report.Add("====================================================\n");
        report.Add("# FOUND NO MODS THAT ARE INCOMPATIBLE OR CONFLICT WITH YOUR OTHER MODS # \n");
        report.Add("\n");
        
        // Plugin and FormID analysis
        var pluginResult = AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Plugin Analyzer");
        var formidResult = AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "FormId Analyzer");
        var recordResult = AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Record Scanner");
        
        report.Add("====================================================\n");
        report.Add("SCANNING THE LOG FOR SPECIFIC (POSSIBLE) SUSPECTS...\n");
        report.Add("====================================================\n");
        
        // Plugin suspects
        report.Add("# LIST OF (POSSIBLE) PLUGIN SUSPECTS #\n");
        if (pluginResult != null && pluginResult.HasFindings)
        {
            report.AddRange(pluginResult.ReportLines);
        }
        else
        {
            report.Add("* COULDN'T FIND ANY PLUGIN SUSPECTS *\n");
        }
        report.Add("\n\n");
        
        // FormID suspects
        report.Add("# LIST OF (POSSIBLE) FORM ID SUSPECTS #\n");
        if (formidResult != null && formidResult.HasFindings)
        {
            report.AddRange(formidResult.ReportLines);
        }
        else
        {
            report.Add("* COULDN'T FIND ANY FORM ID SUSPECTS *\n");
        }
        report.Add("\n\n");
        
        // Records
        report.Add("# LIST OF DETECTED (NAMED) RECORDS #\n");
        if (recordResult != null && recordResult.HasFindings)
        {
            report.AddRange(recordResult.ReportLines);
        }
        else
        {
            report.Add("* COULDN'T FIND ANY NAMED RECORDS *\n");
        }
        report.Add("\n\n");
        
        // Footer
        var footer = GenerateFooter();
        report.Add(footer);
        
        return string.Join("", report);
    }

    /// <summary>
    /// Generate footer for the report with Scanner 111 branding
    /// </summary>
    /// <returns>Footer text</returns>
    private string GenerateFooter()
    {
        var footer = new List<string>();
        
        // Check if this is Fallout 4 to include game-specific footer
        var detectedGame = DetectGameFromCrashLog();
        if (detectedGame?.Replace(" ", "").Equals("Fallout4", StringComparison.OrdinalIgnoreCase) == true)
        {
            footer.Add("FOR FULL LIST OF MODS THAT CAUSE PROBLEMS, THEIR ALTERNATIVES AND DETAILED SOLUTIONS\n");
            footer.Add("VISIT THE BUFFOUT 4 CRASH ARTICLE: https://www.nexusmods.com/fallout4/articles/3115\n");
            footer.Add("===============================================================================\n");
            footer.Add("Scanner 111 by evildarkarchon | Support: https://discord.gg/pF9U5FmD6w\n");
            footer.Add("Scanner 111 is a successor to the CLASSIC application by Poet (guidance.of.grace): https://www.nexusmods.com/fallout4/mods/56255\n");
            footer.Add("CONTRIBUTORS to CLASSIC: evildarkarchon | kittivelae | AtomicFallout757 | wxMichael\n");
            footer.Add("Scanner 111 | https://github.com/evildarkarchon/Scanner111\n");
        }
        
        // Universal footer with Scanner 111 branding
        var buildDate = GetBuildDate().ToString("yy.MM.dd");
        footer.Add($"Scanner 111 v1.0.0 | {buildDate} | END OF AUTOSCAN \n");
        
        return string.Join("", footer);
    }

    /// <summary>
    /// Detect game type from crash log information
    /// </summary>
    /// <returns>Game name or null if not detected</returns>
    private string? DetectGameFromCrashLog()
    {
        if (CrashLog?.GameVersion != null)
        {
            var gameVersion = CrashLog.GameVersion.ToLower();
            if (gameVersion.Contains("fallout 4") || gameVersion.Contains("fallout4"))
                return "Fallout4";
            if (gameVersion.Contains("skyrim"))
                return "Skyrim";
        }
        
        // Default to Fallout4 if not detected (matches Python behavior)
        return "Fallout4";
    }

    /// <summary>
    /// Get the build date of the assembly
    /// </summary>
    /// <returns>Build date</returns>
    private static DateTime GetBuildDate()
    {
        try
        {
            // Get the assembly location
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var location = assembly.Location;
            
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                // Return the last write time of the assembly file
                return File.GetLastWriteTime(location);
            }
        }
        catch
        {
            // Fallback if reflection fails
        }
        
        // Fallback to current date if we can't determine build date
        return DateTime.Now;
    }
}

/// <summary>
/// Status of a scan operation
/// </summary>
public enum ScanStatus
{
    /// <summary>
    /// Scan is pending
    /// </summary>
    Pending,
    
    /// <summary>
    /// Scan is in progress
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Scan completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Scan completed with some errors
    /// </summary>
    CompletedWithErrors,
    
    /// <summary>
    /// Scan failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Scan was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// Statistics dictionary for scan operations
/// </summary>
public class ScanStatistics : Dictionary<string, int>
{
    /// <summary>
    /// Initialize with default counters
    /// </summary>
    public ScanStatistics()
    {
        this["scanned"] = 0;
        this["incomplete"] = 0;
        this["failed"] = 0;
    }
    
    /// <summary>
    /// Number of files scanned
    /// </summary>
    public int Scanned
    {
        get => this["scanned"];
        set => this["scanned"] = value;
    }
    
    /// <summary>
    /// Number of incomplete crash logs
    /// </summary>
    public int Incomplete
    {
        get => this["incomplete"];
        set => this["incomplete"] = value;
    }
    
    /// <summary>
    /// Number of failed scans
    /// </summary>
    public int Failed
    {
        get => this["failed"];
        set => this["failed"] = value;
    }
    
    /// <summary>
    /// Increment a counter by 1
    /// </summary>
    public void Increment(string key)
    {
        if (ContainsKey(key))
            this[key]++;
        else
            this[key] = 1;
    }
}