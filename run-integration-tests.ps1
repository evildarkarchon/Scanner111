#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs integration tests that may involve I/O and external dependencies.

.DESCRIPTION
    This script runs tests marked with Category=Integration, which typically
    involve file system operations, database access, or other external resources.
    These tests run with limited parallelism to avoid resource conflicts.

.PARAMETER NoBuild
    Skip building the project before running tests.

.PARAMETER Sequential
    Run tests sequentially instead of in parallel.

.PARAMETER Verbose
    Show detailed test output.

.EXAMPLE
    ./run-integration-tests.ps1
    Runs all integration tests with default parallelism.

.EXAMPLE
    ./run-integration-tests.ps1 -Sequential -Verbose
    Runs integration tests one at a time with detailed output.
#>

param(
    [switch]$NoBuild,
    [switch]$Sequential,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Running Integration Tests" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

# Set environment variable for sequential execution if requested
if ($Sequential) {
    $env:XUNIT_MAXPARALLEL = "1"
    Write-Host "Running tests sequentially..." -ForegroundColor Gray
}

# Build arguments
$verbosity = if ($Verbose) { "normal" } else { "minimal" }
$args = @(
    "test",
    "Scanner111.Test",
    "--filter", "Category=Integration",
    "--verbosity", $verbosity,
    "--logger", "console;verbosity=$verbosity"
)

if ($NoBuild) {
    $args += "--no-build"
}

Write-Host "Starting integration tests..." -ForegroundColor Green
Write-Host "Note: These tests may take longer due to I/O operations." -ForegroundColor Gray

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

& dotnet $args
$exitCode = $LASTEXITCODE

$stopwatch.Stop()
$elapsed = $stopwatch.Elapsed

# Clean up environment variable
if ($Sequential) {
    Remove-Item Env:\XUNIT_MAXPARALLEL -ErrorAction SilentlyContinue
}

Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Execution Time: $($elapsed.Minutes)m $($elapsed.Seconds)s" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($exitCode -ne 0) {
    Write-Host "`n❌ Integration tests failed with exit code: $exitCode" -ForegroundColor Red
    exit $exitCode
} else {
    Write-Host "`n✅ All integration tests passed!" -ForegroundColor Green
}