#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Adds categorization traits to existing test files that don't have them.

.DESCRIPTION
    This script analyzes test files and adds appropriate [Trait] attributes based on:
    - File location (Integration, Services, Analysis, etc.)
    - Test type (Unit vs Integration)
    - Performance characteristics (Fast vs Slow based on I/O operations)
#>

param(
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "Adding test categorization traits to existing test files..." -ForegroundColor Cyan

# Define categorization rules
$categorization = @{
    "Integration\\" = @{
        Category = "Integration"
        Performance = "Slow"
        Component = "Integration"
    }
    "Services\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Service"
    }
    "Analysis\\Analyzers\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Analyzer"
    }
    "Analysis\\Validators\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Validator"
    }
    "Analysis\\SignalProcessing\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "SignalProcessing"
    }
    "Orchestration\\" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "Orchestration"
    }
    "Configuration\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Configuration"
    }
    "Discovery\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Discovery"
    }
    "Reporting\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Reporting"
    }
    "Models\\" = @{
        Category = "Unit"
        Performance = "Fast"
        Component = "Model"
    }
    "Processing\\" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "Processing"
    }
    "Data\\" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "Data"
    }
    "IO\\" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "IO"
    }
}

# Special cases that need different categorization
$specialCases = @{
    "SampleLogAnalysisIntegrationTests.cs" = @{
        Category = "Integration"
        Performance = "Slow"
        Component = "Integration"
    }
    "SampleOutputValidationTests.cs" = @{
        Category = "Integration"
        Performance = "Slow"
        Component = "Integration"
    }
    "SettingsAnalysisIntegrationTests.cs" = @{
        Category = "Integration"
        Performance = "Medium"
        Component = "Integration"
    }
    "DataflowPipelineOrchestratorTests.cs" = @{
        Category = "Integration"
        Performance = "Medium"
        Component = "Orchestration"
    }
    "HighPerformanceFileIOTests.cs" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "IO"
    }
    "MemoryMappedFileHandlerTests.cs" = @{
        Category = "Unit"
        Performance = "Medium"
        Component = "IO"
    }
}

function Get-TestCategory {
    param([string]$FilePath)
    
    $fileName = Split-Path $FilePath -Leaf
    
    # Check special cases first
    if ($specialCases.ContainsKey($fileName)) {
        return $specialCases[$fileName]
    }
    
    # Check path patterns
    foreach ($pattern in $categorization.Keys) {
        if ($FilePath -match $pattern) {
            return $categorization[$pattern]
        }
    }
    
    # Default categorization
    return @{
        Category = "Unit"
        Performance = "Fast"
        Component = "General"
    }
}

function Add-TraitsToFile {
    param(
        [string]$FilePath,
        [hashtable]$Traits
    )
    
    $content = Get-Content $FilePath -Raw
    
    # Check if file already has traits
    if ($content -match '\[Trait\(') {
        if ($Verbose) {
            Write-Host "  Skipping $($FilePath) - already has traits" -ForegroundColor Gray
        }
        return $false
    }
    
    # Find the class declaration
    if ($content -match '(public\s+(sealed\s+|abstract\s+)?(class|interface)\s+\w+Tests[^\{]*)') {
        $classDeclaration = $matches[1]
        
        # Build trait attributes
        $traitsText = @(
            "[Trait(`"Category`", `"$($Traits.Category)`")]",
            "[Trait(`"Performance`", `"$($Traits.Performance)`")]",
            "[Trait(`"Component`", `"$($Traits.Component)`")]"
        ) -join "`n"
        
        # Check if class already has [Collection] attribute
        $hasCollection = $content -match '\[Collection\([^\]]+\)\][^\[]*' + [regex]::Escape($classDeclaration)
        
        if ($hasCollection) {
            # Add traits after [Collection] attribute
            $newContent = $content -replace "(\[Collection\([^\]]+\)\])\s*`n\s*($([regex]::Escape($classDeclaration)))", "`$1`n$traitsText`n`$2"
        } else {
            # Add traits before class declaration
            $newContent = $content -replace [regex]::Escape($classDeclaration), "$traitsText`n$classDeclaration"
        }
        
        if ($DryRun) {
            Write-Host "  Would add to $($FilePath):" -ForegroundColor Yellow
            Write-Host "    Category: $($Traits.Category), Performance: $($Traits.Performance), Component: $($Traits.Component)" -ForegroundColor Gray
        } else {
            Set-Content -Path $FilePath -Value $newContent -NoNewline
            Write-Host "  ✅ Updated $([System.IO.Path]::GetFileName($FilePath))" -ForegroundColor Green
            Write-Host "     Category: $($Traits.Category), Performance: $($Traits.Performance), Component: $($Traits.Component)" -ForegroundColor Gray
        }
        return $true
    }
    
    Write-Warning "Could not find class declaration in $FilePath"
    return $false
}

# Get all test files
$testFiles = Get-ChildItem -Path "Scanner111.Test" -Filter "*Tests.cs" -Recurse | 
    Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" }

$updatedCount = 0
$skippedCount = 0

foreach ($file in $testFiles) {
    $relativePath = $file.FullName.Replace("$PWD\Scanner111.Test\", "")
    $traits = Get-TestCategory -FilePath $relativePath
    
    if (Add-TraitsToFile -FilePath $file.FullName -Traits $traits) {
        $updatedCount++
    } else {
        $skippedCount++
    }
}

Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Files processed: $($testFiles.Count)" -ForegroundColor White
Write-Host "Files updated: $updatedCount" -ForegroundColor Green
Write-Host "Files skipped: $skippedCount" -ForegroundColor Gray

if (-not $DryRun) {
    Write-Host "`n✅ Test categorization complete!" -ForegroundColor Green
    Write-Host "You can now use the following filters:" -ForegroundColor Cyan
    Write-Host '  dotnet test --filter "Category=Unit"' -ForegroundColor Gray
    Write-Host '  dotnet test --filter "Performance=Fast"' -ForegroundColor Gray
    Write-Host '  dotnet test --filter "Component=Analyzer"' -ForegroundColor Gray
} else {
    Write-Host "`nThis was a dry run. Use without -DryRun to apply changes." -ForegroundColor Yellow
}