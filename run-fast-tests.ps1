#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs fast unit tests only for quick feedback during development.

.DESCRIPTION
    This script runs only tests marked with Category=Unit and Performance!=Slow,
    providing rapid feedback during development. Tests run in parallel for speed.

.PARAMETER NoBuild
    Skip building the project before running tests.

.PARAMETER Filter
    Additional filter to apply to test selection.

.PARAMETER ShowTime
    Display execution time after completion.

.EXAMPLE
    ./run-fast-tests.ps1
    Runs all fast unit tests.

.EXAMPLE
    ./run-fast-tests.ps1 -Filter "PluginAnalyzer"
    Runs only fast unit tests related to PluginAnalyzer.
#>

param(
    [switch]$NoBuild,
    [string]$Filter = "",
    [switch]$ShowTime
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Running Fast Unit Tests" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

# Build filter string
$testFilter = "Category=Unit&Performance!=Slow"
if ($Filter) {
    $testFilter += "&$Filter"
}

# Build arguments
$args = @(
    "test",
    "Scanner111.Test",
    "--filter", $testFilter,
    "--verbosity", "minimal",
    "--logger", "console;verbosity=minimal"
)

if ($NoBuild) {
    $args += "--no-build"
}

Write-Host "`nFilter: $testFilter" -ForegroundColor Gray
Write-Host "Running tests..." -ForegroundColor Green

# Run tests with timing if requested
if ($ShowTime) {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    & dotnet $args
    $exitCode = $LASTEXITCODE
    
    $stopwatch.Stop()
    $elapsed = $stopwatch.Elapsed
    
    Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Execution Time: $($elapsed.Minutes)m $($elapsed.Seconds)s" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
} else {
    & dotnet $args
    $exitCode = $LASTEXITCODE
}

# Check for test failures
if ($exitCode -ne 0) {
    Write-Host "`n❌ Tests failed with exit code: $exitCode" -ForegroundColor Red
    exit $exitCode
} else {
    Write-Host "`n✅ All fast tests passed!" -ForegroundColor Green
}