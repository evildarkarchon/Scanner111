#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs all tests with code coverage analysis and generates an HTML report.

.DESCRIPTION
    This script executes the full test suite with code coverage collection,
    then generates a detailed HTML report showing coverage percentages for
    each class, method, and line of code.

.PARAMETER Filter
    Optional filter to run coverage on specific tests only.

.PARAMETER OpenReport
    Automatically open the HTML report in the default browser after generation.

.PARAMETER MinCoverage
    Minimum required coverage percentage (fails if not met).

.PARAMETER NoBuild
    Skip building the project before running tests.

.EXAMPLE
    ./run-coverage.ps1
    Runs all tests with coverage and generates report.

.EXAMPLE
    ./run-coverage.ps1 -OpenReport -MinCoverage 80
    Runs tests, opens report, and fails if coverage < 80%.

.EXAMPLE
    ./run-coverage.ps1 -Filter "Category=Unit"
    Runs coverage analysis only on unit tests.
#>

param(
    [string]$Filter = "",
    [switch]$OpenReport,
    [int]$MinCoverage = 0,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Code Coverage Analysis" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

# Check if ReportGenerator is installed
$reportGeneratorInstalled = $false
try {
    & dotnet tool list -g | Select-String "reportgenerator" | Out-Null
    $reportGeneratorInstalled = $?
} catch {
    $reportGeneratorInstalled = $false
}

if (-not $reportGeneratorInstalled) {
    Write-Host "`nReportGenerator tool not found. Installing..." -ForegroundColor Yellow
    & dotnet tool install -g dotnet-reportgenerator-globaltool
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install ReportGenerator. Please install manually: dotnet tool install -g dotnet-reportgenerator-globaltool"
    }
}

# Clean previous coverage results
$testResultsDir = "TestResults"
$coverageReportDir = "CoverageReport"

if (Test-Path $testResultsDir) {
    Write-Host "Cleaning previous test results..." -ForegroundColor Gray
    Remove-Item -Path $testResultsDir -Recurse -Force
}

if (Test-Path $coverageReportDir) {
    Write-Host "Cleaning previous coverage report..." -ForegroundColor Gray
    Remove-Item -Path $coverageReportDir -Recurse -Force
}

# Build test arguments
$args = @(
    "test",
    "Scanner111.Test",
    "--collect:XPlat Code Coverage",
    "--results-directory", $testResultsDir,
    "--verbosity", "minimal",
    "--logger", "console;verbosity=minimal"
)

if ($Filter) {
    $args += "--filter", $Filter
    Write-Host "Filter: $Filter" -ForegroundColor Gray
}

if ($NoBuild) {
    $args += "--no-build"
}

Write-Host "`nRunning tests with code coverage..." -ForegroundColor Green

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

& dotnet $args
$testExitCode = $LASTEXITCODE

$stopwatch.Stop()
$testElapsed = $stopwatch.Elapsed

if ($testExitCode -ne 0) {
    Write-Host "`n❌ Tests failed with exit code: $testExitCode" -ForegroundColor Red
    exit $testExitCode
}

Write-Host "`n✅ Tests completed in $($testElapsed.Minutes)m $($testElapsed.Seconds)s" -ForegroundColor Green

# Find coverage files
Write-Host "`nSearching for coverage files..." -ForegroundColor Gray
$coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse

if ($coverageFiles.Count -eq 0) {
    Write-Error "No coverage files found. Make sure tests are generating coverage data."
}

Write-Host "Found $($coverageFiles.Count) coverage file(s)" -ForegroundColor Gray

# Generate HTML report
Write-Host "`nGenerating coverage report..." -ForegroundColor Green

$reportArgs = @(
    "-reports:$($coverageFiles.FullName -join ';')",
    "-targetdir:$coverageReportDir",
    "-reporttypes:Html;Cobertura;TextSummary",
    "-title:Scanner111 Coverage Report",
    "-assemblyfilters:+Scanner111.Core;-Scanner111.Test*;-*.Tests",
    "-classfilters:+*;-*Tests;-*Test;-*Mock*",
    "-verbosity:Warning"
)

& reportgenerator $reportArgs
$reportExitCode = $LASTEXITCODE

if ($reportExitCode -ne 0) {
    Write-Error "Failed to generate coverage report"
}

# Read and display summary
$summaryFile = Join-Path $coverageReportDir "Summary.txt"
if (Test-Path $summaryFile) {
    Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Coverage Summary" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    $summary = Get-Content $summaryFile
    
    # Extract line coverage percentage
    $lineCoverage = 0
    foreach ($line in $summary) {
        if ($line -match "Line coverage:\s+(\d+\.?\d*)%") {
            $lineCoverage = [decimal]$matches[1]
            Write-Host $line -ForegroundColor $(if ($lineCoverage -ge 80) { "Green" } elseif ($lineCoverage -ge 60) { "Yellow" } else { "Red" })
        } elseif ($line -match "Branch coverage:\s+(\d+\.?\d*)%") {
            $branchCoverage = [decimal]$matches[1]
            Write-Host $line -ForegroundColor $(if ($branchCoverage -ge 70) { "Green" } elseif ($branchCoverage -ge 50) { "Yellow" } else { "Red" })
        }
    }
    
    # Check minimum coverage requirement
    if ($MinCoverage -gt 0) {
        Write-Host "`nMinimum coverage requirement: $MinCoverage%" -ForegroundColor Gray
        if ($lineCoverage -lt $MinCoverage) {
            Write-Host "❌ Coverage ($lineCoverage%) is below minimum requirement ($MinCoverage%)" -ForegroundColor Red
            $coverageFailed = $true
        } else {
            Write-Host "✅ Coverage ($lineCoverage%) meets minimum requirement ($MinCoverage%)" -ForegroundColor Green
        }
    }
}

$reportPath = Join-Path $coverageReportDir "index.html"
Write-Host "`nCoverage report generated: $reportPath" -ForegroundColor Cyan

# Open report if requested
if ($OpenReport) {
    Write-Host "Opening coverage report in browser..." -ForegroundColor Gray
    Start-Process $reportPath
}

# Exit with error if coverage failed
if ($coverageFailed) {
    exit 1
}

Write-Host "`n✅ Coverage analysis complete!" -ForegroundColor Green