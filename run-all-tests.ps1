#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the complete test suite with detailed reporting.

.DESCRIPTION
    This script executes all tests in the Scanner111 test suite, organizing them
    by category and performance characteristics. Provides detailed timing and
    success/failure reporting for each category.

.PARAMETER Category
    Run only tests in a specific category (Unit, Integration, Database, etc.)

.PARAMETER SkipSlow
    Skip tests marked as Performance=Slow

.PARAMETER Verbose
    Show detailed test output

.PARAMETER Coverage
    Include code coverage analysis

.PARAMETER NoBuild
    Skip building the project before running tests

.EXAMPLE
    ./run-all-tests.ps1
    Runs all tests with default settings.

.EXAMPLE
    ./run-all-tests.ps1 -Category Unit -Coverage
    Runs only unit tests with coverage analysis.

.EXAMPLE
    ./run-all-tests.ps1 -SkipSlow -Verbose
    Runs all non-slow tests with detailed output.
#>

param(
    [ValidateSet("", "Unit", "Integration", "Database", "All")]
    [string]$Category = "",
    [switch]$SkipSlow,
    [switch]$Verbose,
    [switch]$Coverage,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# ASCII art banner
Write-Host @"
╔═══════════════════════════════════════════════════════╗
║         Scanner111 Test Suite Runner                  ║
║         Version 1.0 - Q4 Optimization                 ║
╚═══════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Test categories to run
$testCategories = @()
if ($Category -eq "" -or $Category -eq "All") {
    $testCategories = @(
        @{Name="Fast Unit Tests"; Filter="Category=Unit&Performance!=Slow"; Color="Green"},
        @{Name="Slow Unit Tests"; Filter="Category=Unit&Performance=Slow"; Color="Yellow"; SkipIfFast=$true},
        @{Name="Integration Tests"; Filter="Category=Integration"; Color="Blue"},
        @{Name="Database Tests"; Filter="Category=Database"; Color="Magenta"}
    )
} else {
    $testCategories = @(
        @{Name="$Category Tests"; Filter="Category=$Category"; Color="Green"}
    )
}

if ($SkipSlow) {
    $testCategories = $testCategories | Where-Object { -not $_.SkipIfFast }
    Write-Host "Skipping slow tests..." -ForegroundColor Gray
}

# Summary tracking
$results = @()
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Function to run a test category
function Run-TestCategory {
    param(
        [hashtable]$TestCategory
    )
    
    Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor $TestCategory.Color
    Write-Host "  $($TestCategory.Name)" -ForegroundColor White
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor $TestCategory.Color
    Write-Host "Filter: $($TestCategory.Filter)" -ForegroundColor Gray
    
    $verbosity = if ($Verbose) { "normal" } else { "minimal" }
    $args = @(
        "test",
        "Scanner111.Test",
        "--filter", $TestCategory.Filter,
        "--verbosity", $verbosity,
        "--logger", "console;verbosity=$verbosity",
        "--logger", "trx;LogFileName=$($TestCategory.Name -replace ' ', '_').trx"
    )
    
    if ($NoBuild) {
        $args += "--no-build"
    }
    
    if ($Coverage) {
        $args += "--collect:XPlat Code Coverage"
        $args += "--results-directory", "TestResults\$($TestCategory.Name -replace ' ', '_')"
    }
    
    $categoryStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    & dotnet $args
    $exitCode = $LASTEXITCODE
    
    $categoryStopwatch.Stop()
    
    $result = @{
        Name = $TestCategory.Name
        Success = ($exitCode -eq 0)
        ExitCode = $exitCode
        Duration = $categoryStopwatch.Elapsed
    }
    
    if ($exitCode -eq 0) {
        Write-Host "✅ $($TestCategory.Name) completed successfully in $($result.Duration.TotalSeconds.ToString('F2'))s" -ForegroundColor Green
    } else {
        Write-Host "❌ $($TestCategory.Name) failed with exit code $exitCode" -ForegroundColor Red
    }
    
    return $result
}

# Run tests
if (-not $NoBuild) {
    Write-Host "`nBuilding solution..." -ForegroundColor Yellow
    & dotnet build Scanner111.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
    }
}

foreach ($category in $testCategories) {
    $result = Run-TestCategory -TestCategory $category
    $results += $result
}

$totalStopwatch.Stop()

# Display summary
Write-Host "`n╔═══════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    TEST SUMMARY                      ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════╝" -ForegroundColor Cyan

$successCount = ($results | Where-Object { $_.Success }).Count
$failureCount = ($results | Where-Object { -not $_.Success }).Count

Write-Host "`nCategories Run: $($results.Count)" -ForegroundColor White
Write-Host "Passed: $successCount" -ForegroundColor Green
Write-Host "Failed: $failureCount" -ForegroundColor $(if ($failureCount -eq 0) { "Gray" } else { "Red" })

Write-Host "`n┌─────────────────────────────────────────────────────┐" -ForegroundColor Gray
Write-Host "│ Category                    Duration      Status    │" -ForegroundColor Gray
Write-Host "├─────────────────────────────────────────────────────┤" -ForegroundColor Gray

foreach ($result in $results) {
    $name = $result.Name.PadRight(25)
    if ($name.Length -gt 25) { $name = $name.Substring(0, 25) }
    $duration = "$($result.Duration.Minutes)m $($result.Duration.Seconds)s".PadRight(10)
    $status = if ($result.Success) { "✅ PASS" } else { "❌ FAIL" }
    $statusColor = if ($result.Success) { "Green" } else { "Red" }
    
    Write-Host "│ " -NoNewline -ForegroundColor Gray
    Write-Host $name -NoNewline
    Write-Host " " -NoNewline
    Write-Host $duration -NoNewline -ForegroundColor Yellow
    Write-Host " " -NoNewline
    Write-Host $status -NoNewline -ForegroundColor $statusColor
    Write-Host "   │" -ForegroundColor Gray
}

Write-Host "└─────────────────────────────────────────────────────┘" -ForegroundColor Gray

Write-Host "`nTotal Execution Time: $($totalStopwatch.Elapsed.Minutes)m $($totalStopwatch.Elapsed.Seconds)s" -ForegroundColor Yellow

# Generate coverage report if requested
if ($Coverage -and $successCount -gt 0) {
    Write-Host "`nGenerating coverage report..." -ForegroundColor Green
    & ./run-coverage.ps1 -NoBuild
}

# Exit with failure if any tests failed
if ($failureCount -gt 0) {
    Write-Host "`n❌ Test suite failed!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n✅ All tests passed!" -ForegroundColor Green
    
    # Performance report for successful runs
    $avgDuration = ($results | Measure-Object -Property { $_.Duration.TotalSeconds } -Average).Average
    Write-Host "Average category duration: $([math]::Round($avgDuration, 2))s" -ForegroundColor Gray
    
    if ($totalStopwatch.Elapsed.TotalSeconds -lt 30) {
        Write-Host "🚀 Excellent performance - tests completed in under 30 seconds!" -ForegroundColor Cyan
    } elseif ($totalStopwatch.Elapsed.TotalSeconds -lt 120) {
        Write-Host "⚡ Good performance - tests completed in under 2 minutes" -ForegroundColor Green
    }
}