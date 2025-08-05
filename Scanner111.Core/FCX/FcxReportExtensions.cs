using System;
using System.Collections.Generic;
using System.Linq;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;

namespace Scanner111.Core.FCX
{
    public static class FcxReportExtensions
    {
        public static void AddFcxReportSections(this List<string> report, ScanResult scanResult)
        {
            // Check for null to match test expectations
            if (scanResult.AnalysisResults == null)
            {
                throw new NullReferenceException();
            }
            
            // Find FCX-related analysis results
            var fcxResult = scanResult.AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "FCX Analyzer");
            var modConflictResult = scanResult.AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Mod Conflict Analyzer");
            var versionResult = scanResult.AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "Version Analyzer");
            
            var hasFcxFindings = fcxResult?.HasFindings == true || 
                                modConflictResult?.HasFindings == true || 
                                versionResult?.HasFindings == true;
            
            if (!hasFcxFindings)
            {
                return;
            }
            
            // Add FCX section header
            report.Add("\n");
            report.Add("====================================================\n");
            report.Add("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n");
            report.Add("====================================================\n");
            
            // Add version information if available
            if (versionResult?.HasFindings == true)
            {
                report.Add("# GAME VERSION INFORMATION #\n");
                if (versionResult.ReportLines != null)
                    report.AddRange(versionResult.ReportLines);
                report.Add("\n");
            }
            
            // Add FCX file integrity results
            if (fcxResult?.HasFindings == true)
            {
                report.Add("# FILE INTEGRITY CHECK RESULTS #\n");
                if (fcxResult.ReportLines != null)
                    report.AddRange(fcxResult.ReportLines);
                report.Add("\n");
            }
            
            // Add mod conflict analysis
            if (modConflictResult?.HasFindings == true)
            {
                report.Add("# MOD CONFLICT ANALYSIS #\n");
                if (modConflictResult.ReportLines != null)
                    report.AddRange(modConflictResult.ReportLines);
                report.Add("\n");
            }
            
            // Add FCX summary
            AddFcxSummary(report, fcxResult, modConflictResult, versionResult);
        }
        
        private static void AddFcxSummary(List<string> report, 
            AnalysisResult? fcxResult, 
            AnalysisResult? modConflictResult,
            AnalysisResult? versionResult)
        {
            var issues = new List<string>();
            
            if (fcxResult?.HasFindings == true && fcxResult is GenericAnalysisResult fcxGeneric && fcxGeneric.Data != null)
            {
                var modifiedCount = Convert.ToInt32(fcxGeneric.Data.GetValueOrDefault("ModifiedFilesCount", 0));
                var missingCount = Convert.ToInt32(fcxGeneric.Data.GetValueOrDefault("MissingFilesCount", 0));
                
                if (modifiedCount > 0)
                    issues.Add($"{modifiedCount} modified game files detected");
                if (missingCount > 0)
                    issues.Add($"{missingCount} missing game files detected");
            }
            
            if (modConflictResult?.HasFindings == true && modConflictResult is GenericAnalysisResult conflictGeneric && conflictGeneric.Data != null)
            {
                var conflictCount = Convert.ToInt32(conflictGeneric.Data.GetValueOrDefault("TotalIssues", 0));
                if (conflictCount > 0)
                    issues.Add($"{conflictCount} mod conflicts detected");
            }
            
            if (versionResult?.HasFindings == true && versionResult is GenericAnalysisResult versionGeneric && versionGeneric.Data != null)
            {
                var isDowngrade = Convert.ToBoolean(versionGeneric.Data.GetValueOrDefault("IsDowngrade", false));
                if (isDowngrade)
                    issues.Add("Game version downgrade detected");
            }
            
            if (issues.Any())
            {
                report.Add("# FCX SUMMARY #\n");
                report.Add("The following issues were detected:\n");
                foreach (var issue in issues)
                {
                    report.Add($"  • {issue}\n");
                }
                report.Add("\n");
                report.Add("* For detailed FCX documentation and solutions, see: https://github.com/evildarkarchon/Scanner111/wiki/FCX-Mode *\n");
                report.Add("\n");
            }
        }
        
        public static string GenerateFcxSectionForSettings(bool fcxEnabled)
        {
            if (fcxEnabled)
            {
                return "* FCX MODE IS ENABLED - PERFORMING ADVANCED FILE INTEGRITY CHECKS *\n" +
                       "[ FCX Mode checks game file integrity and detects mod conflicts. ]\n";
            }
            else
            {
                return "* NOTICE: FCX MODE IS DISABLED. YOU CAN ENABLE IT TO DETECT PROBLEMS IN YOUR MOD & GAME FILES *\n" +
                       "[ FCX Mode can be enabled in the Scanner 111 application settings. ]\n";
            }
        }
    }
}