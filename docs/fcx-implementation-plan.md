# FCX and Game Integrity Implementation Plan

## Goals:
### 1. Multi-Version Support

Fallout 4: 2 versions (1.10.163.0 and 1.10.984.0)
Skyrim SE: 4 versions including platform variants (Steam vs GOG)
Each version has specific hash identification and compatibility notes

### 2. Version Detection System

SHA256 hash-based version identification
Platform detection (Steam/GOG)
Clear reporting of which version is installed
Mod compatibility guidance based on version

### 3. XSE Compatibility Management

Version-specific Script Extender requirements
Compatibility warnings for mismatched XSE versions
Recommendations for the correct XSE version per game version

### 4. User-Friendly Reporting
The system respects that many modders intentionally stay on older versions:

Shows "best mod compatibility" notes for legacy versions
No pressure to update - just informative messages
Clear version identification in reports

### 5. Code Architecture

GameVersionInfo class for version metadata
XseVersionRequirement for version-specific requirements
Static data classes for easy maintenance
Extensible design for future versions

## Summary
The FCX implementation will provide robust support for multiple game versions, ensuring modders can work with their preferred setups without compatibility issues. The design will focus on clear reporting, version detection, and user-friendly features, while maintaining a clean architecture that allows for easy extension in the future.

Note: The SHA256 hashes in the code are placeholders and will need to be collected from verified game installations before deployment.

## Phase 1: Core Infrastructure Setup (Week 1-2)

### 1.1 Extend Models and Data Structures
- **Extend `CrashLog` model** to include game-specific metadata
- **Create new models**:
  ```csharp
  public class GameConfiguration
  {
      public string GameName { get; set; }
      public string RootPath { get; set; }
      public string ExecutablePath { get; set; }
      public string DocumentsPath { get; set; }
      public Dictionary<string, string> FileHashes { get; set; }
  }
  
  public class FcxScanResult : AnalysisResult
  {
      public List<FileIntegrityCheck> FileChecks { get; set; }
      public List<HashValidation> HashValidations { get; set; }
      public GameIntegrityStatus GameStatus { get; set; }
  }
  ```

### 1.2 Port YAML Configuration System (Some of this is already done, but could need extending, existing names might be different)
- **Leverage existing `IYamlSettingsProvider`** infrastructure
- **Create game-specific YAML models**:
  ```csharp
  public class ClassicMainYaml
  {
      public ClassicInfo ClassicInfo { get; set; }
      public Dictionary<string, string> ModsCore { get; set; }
      public Dictionary<string, string> ModsFreq { get; set; }
      // ... other sections
  }
  
  public class ClassicGameYaml  // For Fallout4, Skyrim, etc.
  {
      public Dictionary<string, string> XseHashedScripts { get; set; }
      // ... game-specific data
  }
  ```

### 1.3 Extend Game Path Detection
- **Enhance existing `GamePathDetection` class**:
  - Add support for multiple games (Fallout 4 and Skyrim SE)
  - Implement XSE log parsing for all supported games
  - Add Steam/GOG/Epic Games launcher detection
  - Implement Documents folder detection for INI files

### 1.4 Version Detection System
- **Create comprehensive version detection**:
  - Support multiple game versions per title
  - Track version-specific requirements (XSE compatibility)
  - Store SHA256 hashes for all known versions
  - Provide mod compatibility guidance based on version
- **Supported versions**:
  - Fallout 4: 1.10.163.0, 1.10.984.0
  - Skyrim SE: 1.5.97.0, 1.6.640.0 (Steam), 1.6.1170.0 (Steam), 1.6.1179.0 (GOG)

## Phase 2: FCX Core Functionality (Week 2-3)

### 2.1 Create File Integrity Analyzer
```csharp
public class FileIntegrityAnalyzer : IAnalyzer
{
    public string Name => "FCX File Integrity";
    public int Priority => 10;  // Run after basic analyzers
    public bool CanRunInParallel => true;
    
    public async Task<AnalysisResult> AnalyzeAsync(
        CrashLog crashLog, 
        CancellationToken cancellationToken)
    {
        // Implement file checking logic
    }
}
```

### 2.2 Create Hash Validation Service
```csharp
public interface IHashValidationService
{
    Task<string> CalculateFileHashAsync(string filePath, CancellationToken ct);
    Task<HashValidationResult> ValidateFileAsync(
        string filePath, 
        string expectedHash, 
        CancellationToken ct);
    Task<Dictionary<string, HashValidationResult>> ValidateBatchAsync(
        Dictionary<string, string> fileHashMap, 
        CancellationToken ct);
}
```

### 2.3 Port Core FCX Checks
- **Game executable validation** (version checking via hash)
  - Detect specific game version from known hashes
  - Report version-specific mod compatibility notes
  - Warn about unofficial/pirated versions
- **XSE (Script Extender) validation**:
  - F4SE for Fallout 4 (version-specific requirements)
  - SKSE for Skyrim SE (version-specific requirements)
  - Check XSE version compatibility with game version
  - Provide download links for correct XSE version
- **Core mod file validation** (Buffout, Address Library, etc.)
- **INI file validation** (syntax and required settings)

## Phase 3: Integration with Scanner111 Pipeline (Week 3-4)

### 3.1 Create FCX Pipeline Decorator (Use Enhanced Scan Pipeline as Base)
```csharp
public class FcxEnabledPipeline : IScanPipeline
{
    private readonly IScanPipeline _innerPipeline;
    private readonly IApplicationSettingsService _settings;
    private readonly IHashValidationService _hashService;
    
    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions options,
        IProgress<BatchProgress> progress,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Run FCX checks before crash log analysis if enabled
        if (_settings.IsFcxEnabled)
        {
            yield return await RunFcxChecksAsync(ct);
        }
        
        // Continue with normal pipeline
        await foreach (var result in _innerPipeline.ProcessBatchAsync(
            logPaths, options, progress, ct))
        {
            yield return result;
        }
    }
}
```

### 3.2 Update Pipeline Builder
```csharp
public class PipelineBuilder
{
    public IScanPipeline Build()
    {
        var pipeline = new ScanPipeline(/* ... */);
        
        if (_enableFcx)
        {
            pipeline = new FcxEnabledPipeline(
                pipeline, 
                _hashService, 
                _settings);
        }
        
        if (_enableCaching)
        {
            pipeline = new EnhancedScanPipeline(/* ... */);
        }
        
        return pipeline;
    }
}
```

## Phase 4: UI Integration (Week 4-5)

### 4.1 Extend Settings View Model
```csharp
public class SettingsViewModel : ViewModelBase
{
    // Existing settings...
    
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public string ModsFolder { get; set; }
    [Reactive] public string IniFolder { get; set; }
    [Reactive] public GameType SelectedGame { get; set; }
}
```

### 4.2 Create FCX Results View
- **Design Avalonia UserControl** for FCX results display
- **Show file integrity status** with visual indicators
- **Display hash validation results** in a grid
- **Provide quick-fix actions** for common issues

### 4.3 Add FCX Commands
```csharp
public class MainViewModel
{
    public ReactiveCommand<Unit, Unit> RunFcxScanCommand { get; }
    public ReactiveCommand<Unit, Unit> BackupGameFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateGameInstallCommand { get; }
}
```

## Phase 5: Advanced Features (Week 5-6)

### 5.1 Backup System
```csharp
public interface IBackupService
{
    Task<BackupResult> BackupGameFilesAsync(
        GameConfiguration config,
        IProgress<BackupProgress> progress,
        CancellationToken ct);
        
    Task<RestoreResult> RestoreBackupAsync(
        string backupPath,
        CancellationToken ct);
}
```

### 5.2 Mod Conflict Detection
- **Port mod scanning logic** from `CLASSIC_ScanGame.py`
- **Implement archive scanning** (BSA/BA2 files), requires `BSArch.exe`
- **Create conflict analyzer** using existing analyzer pattern

### 5.3 Report Generation
- **Extend `ScanResult.GenerateReport()`** to include FCX results
- **Maintain exact output format** from Python version
- **Add FCX-specific report sections**
- **Include version-specific information**:
  - Detected game version with platform (Steam/GOG)
  - Mod compatibility notes for legacy versions
  - XSE version requirements and recommendations

### 5.4 Version Management Features
- **Version downgrade detection** and warnings
- **Mod compatibility database** per version
- **Automatic XSE version matching**
- **Version-specific best practices** in reports

## Phase 6: CLI Support (Week 6)

### 6.1 Add FCX CLI Commands
```csharp
[Command("fcx")]
public class FcxCommand : ICommand
{
    [Option("--game", "Target game")]
    public GameType Game { get; set; }
    
    [Option("--check-only", "Only check, don't fix")]
    public bool CheckOnly { get; set; }
    
    public async Task<int> ExecuteAsync(CancellationToken ct)
    {
        // Implement FCX scan via CLI
    }
}
```

### 6.2 Implement CLI Message Handler
- Extend `CliMessageHandler` for FCX-specific messages
- Add progress reporting for long-running operations

## Implementation Priority & Dependencies

### High Priority (Must Have)
1. Game path detection enhancement
2. File integrity checking
3. Hash validation service
4. Basic FCX analyzer
5. Settings integration

### Medium Priority (Should Have)
1. Backup system
2. Mod conflict detection
3. Advanced reporting
4. UI polish

### Low Priority (Nice to Have)
1. Auto-fix functionality
2. Mod archive scanning
3. Custom game support

## Reusable Scanner111 Infrastructure

### Can Reuse Directly:
- `IScanPipeline` interface and decorators
- `IAnalyzer` pattern for FCX analyzers
- `IYamlSettingsProvider` for configuration
- `ICacheManager` for caching file hashes
- `ResilientExecutor` for retry logic
- `IMessageHandler` for UI abstraction

### Need Minor Extensions:
- `GamePathDetection` - add multi-game support
- `ScanResult` - add FCX-specific properties
- `IApplicationSettingsService` - add FCX settings

### Need to Create:
- Hash calculation utilities
- File backup service
- Game-specific configuration models
- FCX-specific analyzers

## Testing Strategy

### Unit Tests
- Hash calculation accuracy
- File path validation
- YAML parsing for all game configs
- Individual FCX analyzer logic

### Integration Tests
- Full FCX pipeline execution
- Multi-game detection
- Backup and restore operations
- Report generation accuracy

### UI Tests
- Settings persistence
- Progress reporting
- Result display accuracy

## Migration Considerations

1. **Maintain Python compatibility** - outputs must match exactly
2. **Preserve YAML structure** - use same schema as Python
3. **Handle encoding issues** - UTF-8 everywhere with fallbacks
4. **Support relative paths** - for portable installations
5. **Async throughout** - no blocking operations
6. **Version hash collection** - need to collect actual SHA256 hashes for all game versions from the community