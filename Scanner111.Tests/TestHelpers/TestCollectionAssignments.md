# Test Collection Assignments for Scanner111

## Collection Strategy Overview

This document outlines the test collection assignments for the Scanner111 test suite to optimize parallel execution
while preventing resource conflicts.

## Collection Assignments

### 1. **IO Heavy Tests** Collection

Tests that perform intensive file I/O operations and need sequential execution to avoid conflicts.

**Apply to:**

- `Integration/ConcurrencyAndResourceTests.cs` - Already assigned ✓
- `Integration/ReportWritingIntegrationTests.cs` - Heavy file I/O for report generation
- `Integration/FcxReportGenerationTests.cs` - Generates FCX reports with file I/O
- `Integration/MultiGameDetectionTests.cs` - Scans directories for game installations
- `Infrastructure/CrashLogDirectoryManagerTests.cs` - Manages crash log directories
- `Infrastructure/UnsolvedLogsMoverTests.cs` - Moves files between directories
- `Infrastructure/ReportWriterTests.cs` - Writes report files
- `Infrastructure/OPCFilteringTests.cs` - Processes plugin files
- `Infrastructure/HashValidationServiceTests.cs` - Reads files for hash validation
- `Infrastructure/CacheManagerTests.cs` - File-based caching operations

### 2. **FileWatcher Tests** Collection

Tests using FileSystemWatcher need isolation to avoid watcher conflicts.

**Apply to:**

- `CLI/Commands/WatchCommandTests.cs` - Already assigned ✓
- `CLI/Commands/WatchCommandIntegrationTests.cs` - Already assigned ✓
- `CLI/Commands/WatchCommandErrorHandlingTests.cs` - Already assigned ✓
- `CLI/Commands/WatchCommandDashboardTests.cs` - Already assigned ✓

### 3. **Database Tests** Collection

Tests that interact with SQLite databases or shared data stores.

**Apply to:**

- `Services/StatisticsServiceTests.cs` - Already assigned ✓
- `Infrastructure/FormIdDatabaseServiceTests.cs` - Uses SQLite for FormID storage
- `Services/RecentItemsServiceTests.cs` - Persists recent items to storage

### 4. **Settings Tests** Collection

Tests that modify environment variables or application settings.

**Apply to:**

- `Infrastructure/ApplicationSettingsServiceTests.cs` - Already assigned ✓
- `CLI/Services/CliSettingsServiceTests.cs` - Already assigned ✓
- `Infrastructure/SettingsHelperTests.cs` - Modifies settings paths
- `Models/ApplicationSettingsTests.cs` - Tests settings models
- `CLI/Models/CliSettingsTests.cs` - Tests CLI settings
- `GUI/Services/SettingsServiceTests.cs` - GUI settings management

### 5. **Backup Tests** Collection (New)

Tests involving backup and restore operations.

**Apply to:**

- `Infrastructure/BackupServiceTests.cs` - Backup/restore operations

### 6. **ModManager Tests** Collection (New)

Tests interacting with mod managers and game installations.

**Apply to:**

- `ModManagers/ModManagerServiceTests.cs` - Mod manager operations
- `ModManagers/ModManagerDetectorTests.cs` - Detects mod managers
- `ModManagers/ModManagerDisableTests.cs` - Disables mod managers
- `ModManagers/MO2/MO2ModListParserTests.cs` - Parses MO2 mod lists
- `ModManagers/MO2/MO2ProfileReaderTests.cs` - Reads MO2 profiles
- `Infrastructure/GamePathDetectionTests.cs` - Detects game installations
- `Infrastructure/GameVersionDetectionTests.cs` - Detects game versions
- `Models/GameConfigurationTests.cs` - Game configuration
- `Analyzers/FileIntegrityAnalyzerModManagerTests.cs` - File integrity with mod managers
- `Services/ModManagerServiceTests.cs` - Mod manager service operations

### 7. **Terminal UI Tests** Collection (New)

Tests using Spectre.Console terminal UI components.

**Apply to:**

- `CLI/Services/SpectreMessageHandlerTests.cs` - Spectre console output
- `CLI/Services/EnhancedSpectreMessageHandlerTests.cs` - Enhanced console output
- `CLI/Services/SpectreTerminalUIServiceTests.cs` - Terminal UI service
- `CLI/Services/ProgressManagerTests.cs` - Progress display management
- `CLI/InteractiveCommandTests.cs` - Interactive mode
- `Integration/InteractiveModeIntegrationTests.cs` - Interactive mode integration
- `CLI/Integration/MessageHandlerIntegrationTests.cs` - Message handler integration
- `CLI/DemoCommandTests.cs` - Demo command with console output
- `CLI/ConfigCommandTests.cs` - Config command with console interaction

### 8. **Network Tests** Collection (New)

Tests that make HTTP/network calls.

**Apply to:**

- `Services/UpdateServiceTests.cs` - Update checks via HTTP
- `Services/UpdateServiceHttpTests.cs` - HTTP-specific update tests
- `Services/UpdateServiceComprehensiveTests.cs` - Comprehensive update tests
- `Services/UpdateServiceFocusedTests.cs` - Focused update tests

### 9. **GUI Tests** Collection (New)

Tests for Avalonia GUI components.

**Apply to:**

- `GUI/ViewModels/MainWindowViewModelTests.cs` - Main window view model
- `GUI/ViewModels/SettingsWindowViewModelTests.cs` - Settings window view model
- `GUI/ViewModels/StatisticsViewModelTests.cs` - Statistics view model
- `GUI/ViewModels/FcxResultViewModelTests.cs` - FCX result view model
- `GUI/Models/ScanResultViewModelTests.cs` - Scan result view model
- `GUI/Services/GuiMessageHandlerServiceTests.cs` - GUI message handler
- `GUI/Services/ThemeServiceTests.cs` - Theme service
- `GUI/Integration/ViewModelServiceIntegrationTests.cs` - View model integration
- `GUI/Converters/AnalysisResultSummaryConverterTests.cs` - Analysis result converter
- `GUI/Converters/BooleanToFindingsColorConverterTests.cs` - Color converter
- `GUI/Converters/BooleanToFindingsTextConverterTests.cs` - Text converter

### 10. **Audio Tests** Collection (New)

Tests involving audio notifications.

**Apply to:**

- `Services/AudioNotificationServiceTests.cs` - Audio notification service

### 11. **Parser Tests** Collection (New)

Tests that parse crash logs and may share sample data.

**Apply to:**

- `Infrastructure/CrashLogParserTests.cs` - Crash log parsing
- `Infrastructure/CrashLogParserAdditionalTests.cs` - Additional parser tests
- `Infrastructure/YamlParsingTests.cs` - YAML parsing
- `Infrastructure/YamlUnderscoreNamingTests.cs` - YAML naming conventions
- `Infrastructure/ClassicFallout4YamlV2DeserializationTests.cs` - YAML deserialization

### 12. **Pipeline Tests** Collection (New)

Tests for the analysis pipeline that orchestrate multiple components.

**Apply to:**

- `Pipeline/ScanPipelineTests.cs` - Scan pipeline
- `Pipeline/EnhancedScanPipelineTests.cs` - Enhanced scan pipeline
- `Pipeline/FcxEnabledPipelineTests.cs` - FCX-enabled pipeline
- `Pipeline/ScanPipelineBuilderTests.cs` - Pipeline builder
- `Pipeline/ProgressReportingTests.cs` - Progress reporting
- `Pipeline/AnalyzerFactoryTests.cs` - Analyzer factory

## Tests That Remain Uncollected (Fully Parallel)

These tests are independent and can run in parallel without conflicts:

### Analyzer Tests (Pure Logic, No I/O)

- `Analyzers/BuffoutVersionAnalyzerV2Tests.cs`
- `Analyzers/FileIntegrityAnalyzerTests.cs`
- `Analyzers/FormIdAnalyzerTests.cs`
- `Analyzers/PluginAnalyzerTests.cs`
- `Analyzers/RecordScannerTests.cs`
- `Analyzers/SettingsScannerTests.cs`
- `Analyzers/SuspectScannerTests.cs`

### FCX Tests (Mostly Mocked)

- `FCX/FcxReportExtensionsTests.cs`
- `FCX/ModCompatibilityServiceTests.cs`
- `FCX/ModConflictAnalyzerTests.cs`
- `FCX/ModScannerTests.cs`
- `FCX/VersionAnalyzerTests.cs`

### CLI Tests (Pure Logic)

- `CLI/AboutCommandTests.cs`
- `CLI/Commands/StatsCommandTests.cs`
- `CLI/FcxCommandTests.cs`
- `CLI/FcxOptionsTests.cs`
- `CLI/Models/WatchOptionsTests.cs`
- `CLI/ProgramServiceConfigurationTests.cs`
- `CLI/ScanCommandTests.cs`
- `CLI/ScanCommandFcxTests.cs`
- `CLI/Services/CircularBufferTests.cs`
- `CLI/Services/MessageLoggerTests.cs`

### Infrastructure Tests (Pure Logic)

- `Infrastructure/CliMessageHandlerTests.cs`
- `Infrastructure/MessageHandlerTests.cs`
- `Infrastructure/GuardTests.cs`
- `Infrastructure/ErrorHandlingTests.cs`
- `Infrastructure/CancellationSupportTests.cs`
- `Infrastructure/NullImplementationsTests.cs`

### Model Tests (Pure Logic)

- `Models/CrashLogTests.cs`
- `Models/ScanResultTests.cs`

### Test Helpers (Not Test Classes)

- `TestHelpers/AnalyzerTestBase.cs`
- `TestHelpers/SettingsTestBase.cs`
- `TestHelpers/SettingsTestCollection.cs`
- `TestHelpers/TestCollections.cs`
- `TestHelpers/TestImplementations.cs`
- `TestHelpers/TestMessageCapture.cs`
- `CLI/TestHelpers/SpectreTestHelper.cs`
- `GUI/TestHelpers/MockServices.cs`

## Expected Performance Impact

### Before Optimization

- All 1493 tests run with minimal parallelization
- Many tests conflict due to shared resources
- Flaky tests due to race conditions
- Estimated runtime: 2-3 minutes

### After Optimization

- **12 sequential collections**: ~40% of tests (600 tests)
- **Fully parallel tests**: ~60% of tests (893 tests)
- Collections run in parallel with each other
- Tests within collections run sequentially
- Estimated runtime: 45-60 seconds (50-66% improvement)

### Key Benefits

1. **Elimination of flaky tests** - Resource conflicts resolved
2. **Faster execution** - Maximum safe parallelization
3. **Predictable results** - No race conditions
4. **Easy maintenance** - Clear organization by resource type
5. **Scalability** - Easy to add new tests to appropriate collections

## Implementation Notes

1. Add `[Collection("CollectionName")]` attribute to each test class
2. Tests without collection attributes run fully parallel
3. Use collection fixtures for shared setup/teardown
4. Monitor test execution times to identify bottlenecks
5. Consider splitting large test classes if they become collection bottlenecks