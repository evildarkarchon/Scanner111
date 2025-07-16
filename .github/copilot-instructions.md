# Scanner111 Copilot Instructions

## Architecture Overview

This is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim). The solution uses a **modular analyzer pattern** with orchestration pipeline for processing crash logs from Buffout 4/Crash Logger.

### Project Structure
- **Scanner111.Core**: Main library with analyzers, models, and infrastructure
- **Scanner111.GUI**: Avalonia MVVM desktop application
- **Scanner111.CLI**: Console application with CommandLineParser
- **Scanner111.Tests**: xUnit test project

### Key Architecture Patterns

**Analyzer Chain Pattern**: Each analyzer implements `IAnalyzer` interface for processing crash logs:
```csharp
public interface IAnalyzer
{
    string Name { get; }
    Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
}
```

**Orchestrator Pattern**: `ScanOrchestrator` coordinates analyzer execution and report generation in a specific order (Plugin → FormID → Suspect → Record → Settings).

**Message Handler Pattern**: Static `MessageHandler` class provides unified messaging across GUI/CLI with `MessageTarget` enum for routing.

**Configuration Injection Pattern**: `ClassicScanLogsInfo` configuration objects are passed through constructor parameters instead of using global state.

**YAML Configuration**: Uses `YamlSettingsCache` for loading settings from `CLASSIC Data/databases/*.yaml` files with dot notation key paths.

## Critical Implementation Details

### Python Compatibility Requirements
- **Exact Output Matching**: C# reports must match Python output character-for-character
- **CLASSIC Naming**: Keep "CLASSIC" in internal strings/classes for config compatibility
- **UTF-8 Encoding**: Always use UTF-8 with error handling for file operations
- **FormID Pattern**: Use regex `^\s*Form ID:\s*0x([0-9A-F]{8})` for extraction

### Key Data Structures
```csharp
// Primary crash log model
public class CrashLog
{
    public List<string> OriginalLines { get; init; } = new();
    public Dictionary<string, string> Plugins { get; set; } = new(); // filename -> loadOrder
    public List<string> CallStack { get; set; } = new();
    public string MainError { get; set; } = string.Empty;
}

// Configuration loaded from YAML
public class ClassicScanLogsInfo
{
    public Dictionary<string, string> SuspectsErrorList { get; set; } = new();
    public Dictionary<string, string> SuspectsStackList { get; set; } = new();
    public List<string> IgnorePluginsList { get; set; } = new();
}
```

### Threading & Performance
- Use `ConfigureAwait(false)` in library code
- Implement semaphore-based concurrency control for batch processing
- Thread-safe log cache with `ThreadSafeLogCache` pattern
- Async/await throughout pipeline with cancellation token support

## Development Workflows

### Build & Test
```bash
dotnet build Scanner111.sln
dotnet test Scanner111.Tests/
```

### Running Applications
```bash
# GUI
dotnet run --project Scanner111.GUI

# CLI
dotnet run --project Scanner111.CLI -- scan
```

### Key Files to Reference
- `classic-csharp-ai-implementation-guide.md`: Complete porting guide with exact implementation requirements
- `Code to Port/ClassicLib/ScanLog/`: Original Python analyzers for reference
- `sample_logs/`: Real crash logs for testing analyzer accuracy

## Project-Specific Conventions

### Naming Conventions
- **External**: Use "Scanner111" in namespaces, project names, window titles
- **Internal**: Keep "CLASSIC" in class names, YAML keys, and report text for compatibility
- **Files**: Use Pascal case for C# files, match Python names for ported components

### Error Handling
- Port Python try/except blocks exactly - don't add new error handling
- Use `MessageHandler.MsgError()` for user-facing errors
- Preserve all original error messages for user familiarity

### Configuration Management
- YAML files are the source of truth for all settings
- Use `YamlSettingsCache.YamlSettings<T>()` for loading with caching
- Settings changes must be persisted back to YAML files

### Testing Strategy
- Use real crash logs from `sample_logs/` for integration tests
- Test each analyzer independently with known input/output pairs
- Verify report formatting matches Python output exactly

## Integration Points

### External Dependencies
- **YamlDotNet**: For YAML configuration parsing
- **System.Data.SQLite**: For FormID database lookups
- **Avalonia**: For cross-platform GUI
- **CommandLineParser**: For CLI argument parsing

### Cross-Component Communication
- **MessageHandler**: Unified logging/progress reporting
- **Configuration Injection**: `ClassicScanLogsInfo` objects passed through constructors
- **YamlSettingsCache**: Configuration access layer

### Data Flow
1. **File Discovery**: Find crash logs in configured directories
2. **Parsing**: Extract structured data from log text
3. **Analysis**: Run analyzer chain (Plugin → FormID → Suspect → Record → Settings)
4. **Report Generation**: Create markdown reports matching Python format
5. **Output**: Save to `.md` files with the pattern `<original_filename_without_extension>-REPORT.md`

## Performance Considerations
- Implement async pipeline for large batch processing
- Use memory caching for repeated configuration access
- Throttle concurrent operations to prevent resource exhaustion
- Cache FormID database lookups for better performance
