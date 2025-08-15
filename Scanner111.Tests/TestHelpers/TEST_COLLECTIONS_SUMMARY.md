# Scanner111 Test Collections Summary

## Overview

The Scanner111 test suite has been organized into 12 logical test collections to optimize parallel execution while
preventing resource conflicts. This organization allows tests within the same collection to run sequentially while
different collections run in parallel.

## Quick Statistics

- **Total Tests**: ~1493
- **Test Collections**: 12 (sequential execution within each)
- **Uncollected Tests**: ~893 (fully parallel)
- **Expected Performance Improvement**: 50-66% reduction in test runtime

## Test Collections

### 1. **IO Heavy Tests** (9 test classes)

Tests performing intensive file I/O operations.

- ConcurrencyAndResourceTests ✓
- ReportWritingIntegrationTests
- FcxReportGenerationTests
- MultiGameDetectionTests
- CrashLogDirectoryManagerTests
- UnsolvedLogsMoverTests
- ReportWriterTests
- OPCFilteringTests
- HashValidationServiceTests
- CacheManagerTests

### 2. **FileWatcher Tests** (4 test classes)

Tests using FileSystemWatcher functionality.

- WatchCommandTests ✓
- WatchCommandIntegrationTests ✓
- WatchCommandErrorHandlingTests ✓
- WatchCommandDashboardTests ✓

### 3. **Database Tests** (3 test classes)

Tests interacting with SQLite databases.

- StatisticsServiceTests ✓
- FormIdDatabaseServiceTests
- RecentItemsServiceTests

### 4. **Settings Tests** (6 test classes)

Tests modifying application settings or environment variables.

- ApplicationSettingsServiceTests ✓
- CliSettingsServiceTests ✓
- SettingsHelperTests
- ApplicationSettingsTests (Models)
- CliSettingsTests (Models)
- SettingsServiceTests (GUI)

### 5. **Backup Tests** (1 test class)

Tests for backup/restore operations.

- BackupServiceTests

### 6. **ModManager Tests** (10 test classes)

Tests interacting with mod managers and game installations.

- ModManagerServiceTests (2 instances)
- ModManagerDetectorTests
- ModManagerDisableTests
- MO2ModListParserTests
- MO2ProfileReaderTests
- GamePathDetectionTests
- GameVersionDetectionTests
- GameConfigurationTests
- FileIntegrityAnalyzerModManagerTests

### 7. **Terminal UI Tests** (9 test classes)

Tests using Spectre.Console terminal UI components.

- SpectreMessageHandlerTests
- EnhancedSpectreMessageHandlerTests
- SpectreTerminalUIServiceTests
- ProgressManagerTests
- InteractiveCommandTests
- InteractiveModeIntegrationTests
- MessageHandlerIntegrationTests
- DemoCommandTests
- ConfigCommandTests

### 8. **Network Tests** (4 test classes)

Tests making HTTP/network calls.

- UpdateServiceTests
- UpdateServiceHttpTests
- UpdateServiceComprehensiveTests
- UpdateServiceFocusedTests

### 9. **GUI Tests** (11 test classes)

Tests for Avalonia GUI components.

- MainWindowViewModelTests
- SettingsWindowViewModelTests
- StatisticsViewModelTests
- FcxResultViewModelTests
- ScanResultViewModelTests
- GuiMessageHandlerServiceTests
- ThemeServiceTests
- ViewModelServiceIntegrationTests
- AnalysisResultSummaryConverterTests
- BooleanToFindingsColorConverterTests
- BooleanToFindingsTextConverterTests

### 10. **Audio Tests** (1 test class)

Tests for audio notifications.

- AudioNotificationServiceTests

### 11. **Parser Tests** (5 test classes)

Tests parsing crash logs and YAML files.

- CrashLogParserTests
- CrashLogParserAdditionalTests
- YamlParsingTests
- YamlUnderscoreNamingTests
- ClassicFallout4YamlV2DeserializationTests

### 12. **Pipeline Tests** (6 test classes)

Tests for the analysis pipeline.

- ScanPipelineTests
- EnhancedScanPipelineTests
- FcxEnabledPipelineTests
- ScanPipelineBuilderTests
- ProgressReportingTests
- AnalyzerFactoryTests

## Uncollected Tests (Run Fully Parallel)

The following test categories remain uncollected and can run in parallel:

- **Analyzer Tests**: Pure logic tests for analyzers (7 classes)
- **FCX Tests**: Mostly mocked FCX functionality tests (5 classes)
- **CLI Tests**: Pure logic command tests (10 classes)
- **Infrastructure Tests**: Pure logic infrastructure tests (6 classes)
- **Model Tests**: Data model tests (2 classes)
- **Service Tests**: Pure logic service tests (various)

## Collection Fixtures

Each collection has an associated fixture class that provides:

- Isolated temporary directories for file operations
- Environment variable backup/restore for settings tests
- Resource cleanup on disposal
- Shared setup for tests within the collection

## Benefits

1. **Eliminates Flaky Tests**: Resource conflicts are prevented by sequential execution within collections
2. **Maximizes Parallelization**: Independent tests run in parallel across collections
3. **Predictable Results**: No race conditions or timing-dependent failures
4. **Easy Maintenance**: Clear organization by resource type
5. **Scalability**: Easy to add new tests to appropriate collections

## Usage

Tests are automatically assigned to collections using the `[Collection("CollectionName")]` attribute on test classes.
Tests without this attribute run fully parallel.

Example:

```csharp
[Collection("IO Heavy Tests")]
public class MyFileOperationTests
{
    // Tests that perform file I/O
}
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests in a specific collection
dotnet test --filter "Collection=IO Heavy Tests"

# Run tests with detailed output
dotnet test -v normal

# Run tests with parallel execution info
dotnet test --logger:"console;verbosity=detailed"
```

## Maintenance

When adding new test classes:

1. Identify the primary resource the tests use
2. Add the appropriate `[Collection("name")]` attribute
3. If no collection fits, leave uncollected for parallel execution
4. Consider creating a new collection if you have multiple related test classes with unique resource requirements