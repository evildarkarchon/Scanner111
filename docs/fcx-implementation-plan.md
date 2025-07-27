# FCX and Game Integrity Implementation Plan

## Phase 1: Core Infrastructure Setup (Week 1-2) ✓ COMPLETED

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

### 1.2 Port YAML Configuration System
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
      public List<string> BackupFiles { get; set; }
      // ... game-specific data
  }
  ```

### 1.3 Extend Game Path Detection
- **Enhance existing `GamePathDetection` class**:
  - Improve Fallout 4 detection (already partially implemented)
  - Add F4SE log parsing for better path detection
  - Add Steam/GOG/Epic Games launcher detection
  - Implement Documents folder detection for INI files
  - Design with extensibility for future games

### 1.4 Version Detection System
- **Create comprehensive version detection**:
  - Support multiple Fallout 4 versions
  - Track version-specific requirements (F4SE compatibility)
  - Store SHA256 hashes for all known versions
  - Provide mod compatibility guidance based on version
- **Supported Fallout 4 versions**:
  - 1.10.163.0 - Pre-Next Gen Update (most mod compatible)
  - 1.10.984.0 - Next Gen Update (latest version)

## Phase 2: FCX Core Functionality (Week 2-3) ✓ COMPLETED

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
  - Detect specific Fallout 4 version from known hashes
  - Report version-specific mod compatibility notes
  - Warn about unofficial/pirated versions
- **F4SE (Script Extender) validation**:
  - Check F4SE installation and version
  - Verify compatibility with detected game version
  - Provide download links for correct F4SE version
- **Core mod file validation**:
  - Buffout 4 (crash logger)
  - Address Library
  - Other essential framework mods
- **INI file validation** (syntax and required settings)

## Phase 3: Integration with Scanner111 Pipeline (Week 3-4) ✓ COMPLETED

### 3.1 Create FCX Pipeline Decorator
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

## Phase 4: UI Integration (Week 4-5) ✓ COMPLETED

### 4.1 Extend Settings View Model
```csharp
public class SettingsViewModel : ViewModelBase
{
    // Existing settings...
    
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public string ModsFolder { get; set; }
    [Reactive] public string IniFolder { get; set; }
    // Game selection prepared for future but hidden in UI for now
    // Only Fallout 4 is currently supported
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

## Phase 5: Advanced Features (Week 5-6) ✓ COMPLETED

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
- **Implement archive scanning** (BSA/BA2 files) using `BSArch.exe` as the archive tool
- **Create conflict analyzer** using existing analyzer pattern

### 5.3 Report Generation
- **Extend `ScanResult.GenerateReport()`** to include FCX results
- **Maintain exact output format** from Python version (There might be a bug in the Python version of FCX that caused the report to show, at least some of its output twice)
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
2. **Handle encoding issues** - UTF-8 everywhere with fallbacks
3. **Support relative paths** - for portable installations
4. **Async throughout** - no blocking operations
5. **Version hash collection** - need to collect actual SHA256 hashes for all game versions from the community