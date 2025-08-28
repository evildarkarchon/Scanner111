# Test Infrastructure Migration Guide

## Overview
This guide documents the Q2 Migration Preparation phase implementation for transitioning the Scanner111 test suite from external sample dependencies to self-contained testing.

## New Infrastructure Components

### 1. Embedded Resource Infrastructure
- **Location**: `Infrastructure/TestData/EmbeddedResourceProvider.cs`
- **Purpose**: Provides access to embedded crash log resources
- **Features**:
  - Cached resource loading for performance
  - Support for both crash logs and expected outputs
  - Thread-safe resource access

### 2. Synthetic Data Generation
- **Location**: `Infrastructure/TestData/CrashLogDataGenerator.cs`
- **Purpose**: Generates realistic crash log data using Bogus library
- **Features**:
  - Deterministic generation with seed support
  - Configurable crash scenarios
  - Realistic plugin lists and error patterns
  - Support for edge cases and special scenarios

### 3. Snapshot Testing
- **Location**: `Infrastructure/Snapshots/SnapshotTestBase.cs`
- **Purpose**: Verify.Xunit integration for output validation
- **Features**:
  - Automatic snapshot management
  - Custom converters for Scanner111 types
  - Sensitive data scrubbing
  - Parameterized snapshots for theory tests

### 4. Migration Utilities
- **Location**: `Infrastructure/Migration/TestMigrationHelper.cs`
- **Purpose**: Assists in migrating existing tests
- **Features**:
  - Test file analysis
  - Automated migration suggestions
  - Batch migration support
  - Project file update generation

### 5. New Test Base Classes
- **`EmbeddedResourceTestBase`**: Combines embedded resources with snapshot testing
- **`SnapshotTestBase`**: Pure snapshot testing base

## Migration Process

### Step 1: Migrate Critical Samples
Run the migration utility to copy critical samples to embedded resources:

```bash
dotnet test --filter "FullyQualifiedName=Scanner111.Test.Infrastructure.Migration.MigrateCriticalSamples.MigrateCriticalSamplesToEmbeddedResources"
```

### Step 2: Update Project File
Add embedded resources to `Scanner111.Test.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources/EmbeddedLogs/*.log" />
  <EmbeddedResource Include="Resources/EmbeddedLogs/*.md" />
</ItemGroup>
```

### Step 3: Migrate Test Classes

#### Before (Using SampleDataTestBase):
```csharp
public class MyAnalyzerTests : SampleDataTestBase
{
    [Fact]
    public async Task TestMethod()
    {
        var sampleContent = await ReadSampleLogAsync("crash.log");
        var expectedOutput = await ReadExpectedOutputAsync("crash.log");
        // ... test logic
        ValidateAgainstExpectedOutput(actual, expectedOutput);
    }
}
```

#### After (Using EmbeddedResourceTestBase):
```csharp
public class MyAnalyzerTests : EmbeddedResourceTestBase
{
    [Fact]
    public async Task TestMethod()
    {
        var logContent = await GetEmbeddedLogAsync("crash.log");
        // ... test logic
        await VerifyAsync(result);  // Snapshot testing
    }
}
```

### Step 4: Use Synthetic Data for Edge Cases
```csharp
[Theory]
[ClassData(typeof(SyntheticScenarioTheoryData))]
public async Task TestWithSyntheticData(int seed, CrashLogOptions options)
{
    var crashLog = GenerateDeterministicCrashLog(seed, options);
    // ... test logic
}
```

## Critical Sample Logs

The following logs have been identified as critical for comprehensive testing:

1. **EarlySample** (`crash-2022-06-05-12-52-17.log`) - Early version baseline
2. **WithPluginIssues** (`crash-2023-09-15-01-54-49.log`) - Plugin detection scenarios
3. **WithMemoryIssues** (`crash-2023-11-08-05-46-35.log`) - Memory management problems
4. **RecentSample** (`crash-2024-08-25-11-05-43.log`) - Recent version features
5. **StackOverflow** (`crash-2022-06-09-07-25-03.log`) - Stack overflow scenario
6. **AccessViolation** (`crash-2023-10-14-05-54-22.log`) - Access violation errors
7. **FCXMode** (`crash-2023-10-25-09-49-04.log`) - FCX mode testing
8. **NoBuffout** (`crash-2023-12-01-08-33-44.log`) - Non-Buffout logs
9. **LargeLogFile** (`crash-2022-06-12-07-11-38.log`) - Performance testing
10. **MinimalLog** (`crash-2022-06-15-10-02-51.log`) - Edge case testing

## Benefits of Migration

### Immediate Benefits
- **Portability**: Tests run without external file dependencies
- **Speed**: Embedded resources load faster than file system access
- **Reliability**: No file system permission or path issues
- **Version Control**: Test data versioned with code

### Long-term Benefits
- **Maintainability**: Synthetic data easier to modify for new scenarios
- **Coverage**: Can generate edge cases not present in samples
- **Determinism**: Reproducible test failures with seeded generation
- **CI/CD**: No need to manage sample files in build pipelines

## Migration Checklist

- [ ] Run migration utility to copy critical samples
- [ ] Update project file with embedded resources
- [ ] Migrate test base class inheritance
- [ ] Replace `ReadSampleLogAsync` with `GetEmbeddedLogAsync`
- [ ] Replace output validation with snapshot testing
- [ ] Add synthetic data generation for edge cases
- [ ] Run tests to verify functionality maintained
- [ ] Update CI/CD pipeline if needed
- [ ] Document any test-specific migration notes

## Common Migration Patterns

### Pattern 1: Simple Sample Read
```csharp
// Before
var content = await ReadSampleLogAsync("crash.log");

// After
var content = await GetEmbeddedLogAsync("crash.log");
```

### Pattern 2: Output Validation
```csharp
// Before
ValidateAgainstExpectedOutput(actual, expected);

// After
await VerifyAsync(actual);
```

### Pattern 3: Theory Data
```csharp
// Before
[ClassData(typeof(SampleLogTheoryData))]

// After
[ClassData(typeof(EmbeddedLogTheoryData))]
// Or for synthetic:
[ClassData(typeof(SyntheticScenarioTheoryData))]
```

### Pattern 4: Hybrid Testing
```csharp
// Combine real and synthetic data
var hybridLog = await CreateHybridTestFileAsync(
    CriticalSampleLogs.WithPluginIssues,
    options => {
        options.ErrorType = "EXCEPTION_STACK_OVERFLOW";
        options.PluginCount = 200;
    });
```

## Troubleshooting

### Issue: Embedded resource not found
**Solution**: Ensure the file is marked as EmbeddedResource in .csproj and the namespace matches

### Issue: Snapshot verification fails
**Solution**: Review the .verified files, update if changes are intentional

### Issue: Synthetic data too random
**Solution**: Use deterministic generation with fixed seeds

### Issue: Tests slower after migration
**Solution**: Enable resource preloading in test base class

## Next Steps (Q3: Sample Removal)

After successful migration preparation:
1. Gradually migrate all tests to new infrastructure
2. Monitor test coverage to ensure no regression
3. Remove dependency on sample_logs directory
4. Archive sample files for historical reference
5. Update documentation and CI/CD pipelines