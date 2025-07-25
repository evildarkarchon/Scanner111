using System.Collections.Generic;
using System.Linq;
using Scanner111.Core.Models;

namespace Scanner111.GUI.ViewModels;

/// <summary>
/// ViewModel for displaying FCX (File Integrity Check) results in the UI.
/// </summary>
public class FcxResultViewModel : ViewModelBase
{
    public FcxResultViewModel(FcxScanResult fcxResult)
    {
        FcxResult = fcxResult;
        
        // Calculate summary statistics
        TotalFileChecks = fcxResult.FileChecks?.Count ?? 0;
        PassedChecks = fcxResult.FileChecks?.Count(fc => fc.IsValid) ?? 0;
        FailedChecks = TotalFileChecks - PassedChecks;
        
        TotalHashValidations = fcxResult.HashValidations?.Count ?? 0;
        PassedValidations = fcxResult.HashValidations?.Count(hv => hv.IsValid) ?? 0;
        FailedValidations = TotalHashValidations - PassedValidations;
        
        OverallStatus = fcxResult.GameStatus.ToString();
        HasIssues = FailedChecks > 0 || FailedValidations > 0;
    }
    
    public FcxScanResult FcxResult { get; }
    
    // Summary properties
    public int TotalFileChecks { get; }
    public int PassedChecks { get; }
    public int FailedChecks { get; }
    
    public int TotalHashValidations { get; }
    public int PassedValidations { get; }
    public int FailedValidations { get; }
    
    public string OverallStatus { get; }
    public bool HasIssues { get; }
    
    // Helper properties for UI binding
    public string Summary => $"{PassedChecks}/{TotalFileChecks} file checks passed, {PassedValidations}/{TotalHashValidations} hash validations passed";
    
    public string StatusIcon => HasIssues ? "⚠️" : "✅";
    
    public string StatusColor => HasIssues ? "#FF6B6B" : "#51CF66";
}