# Test Data Management Guide for Scanner111

## Overview

This document outlines the test data management patterns implemented in the Scanner111 test suite, focusing on proper usage of sample crash logs and expected outputs while maintaining test isolation.

## Current Test Data Structure

### Sample Data Locations
- **Sample Logs**: `/sample_logs/FO4/` - Contains real Fallout 4 crash logs (READ-ONLY)
- **Expected Outputs**: `/sample_output/` - Contains expected analysis outputs from legacy CLASSIC scanner (READ-ONLY)
- **Test Data**: `/Scanner111.Test/TestData/` - Contains test-specific YAML and configuration files

### Test Infrastructure Classes

#### 1. SampleDataTestBase
Base class providing utilities for tests that need sample data:
- **Purpose**: Centralizes sample data access and ensures test isolation
- **Location**: `Scanner111.Test/Infrastructure/SampleDataTestBase.cs`
- **Key Features**:
  - Automatic sample directory discovery
  - Test isolation through file copying to temp directories
  - Sample log/output pairing for validation
  - Embedded resource creation for future migration

#### 2. IntegrationTestBase
Base class for all integration tests:
- **Purpose**: Provides common setup, DI configuration, and cleanup
- **Location**: `Scanner111.Test/Infrastructure/IntegrationTestBase.cs`
- **Key Features**:
  - Temporary test directory creation and cleanup
  - Service provider configuration
  - Timeout management
  - Test file creation utilities

## Test Data Usage Patterns

### Pattern 1: Isolated Sample Testing
```csharp
public async Task TestWithSample()
{
    // Copy sample to test directory for isolation
    var samplePath = GetRandomSampleLog();
    var testPath = await CopySampleLogToTestDirAsync(samplePath);
    
    // Perform test operations on copy
    var result = await AnalyzeLog(testPath);
    
    // Original sample remains unchanged
}
```

### Pattern 2: Theory Testing with Sample Data
```csharp
[Theory]
[ClassData(typeof(SampleLogTheoryData))]
public async Task TestMultipleSamples(string logFileName)
{
    var content = await ReadSampleLogAsync(logFileName);
    // Test logic here
}
```

### Pattern 3: Validation Against Expected Output
```csharp
public async Task ValidateOutput()
{
    var pairs = GetMatchingSamplePairs();
    foreach (var (logPath, outputPath) in pairs)
    {
        var actual = await AnalyzeLog(logPath);
        var expected = await File.ReadAllTextAsync(outputPath);
        ValidateAgainstExpectedOutput(actual, expected);
    }
}
```

### Pattern 4: Embedded Resource Preparation
```csharp
public async Task PrepareEmbeddedResource()
{
    // Create embedded copy for when samples are removed
    var embeddedPath = await CreateEmbeddedResourceCopyAsync(
        sampleFilePath: "sample_logs/FO4/crash.log",
        resourceName: "crash-test.log",
        testMethod: nameof(TestMethod));
}
```

## Test Categories Using Sample Data

### 1. Integration Tests
- **Location**: `Scanner111.Test/Integration/`
- **Classes**:
  - `SampleLogAnalysisIntegrationTests` - Tests full analysis pipeline with real logs
  - `SampleOutputValidationTests` - Validates output against expected results
- **Purpose**: End-to-end testing with real data

### 2. Analyzer Tests with Samples
- **Location**: `Scanner111.Test/Analysis/Analyzers/`
- **Example**: `SettingsAnalyzerSampleDataTests`
- **Purpose**: Unit testing with real-world data patterns

### 3. Validation Tests
- **Purpose**: Ensure compatibility with legacy CLASSIC scanner output
- **Approach**: Parse expected outputs and compare key elements

## Test Isolation Strategies

### Strategy 1: File Copying
- Copy sample files to temporary test directories
- Ensures original samples remain unmodified
- Automatic cleanup after test completion

### Strategy 2: In-Memory Processing
- Read sample content into memory
- Process without file system operations where possible
- Suitable for parsing and validation tests

### Strategy 3: Mock File Systems
- Use `IFileIoCore` abstraction for file operations
- Substitute with test implementations
- Complete control over file system behavior

## Migration Strategy for Sample Removal

Since sample directories are marked READ-ONLY and will be removed later:

### 1. Embedded Resources
- Critical test cases should create embedded resource copies
- Store in test assembly as embedded resources
- Use `CreateEmbeddedResourceCopyAsync` during initial test runs

### 2. Test Data Generation
- Create data generators based on sample patterns
- Use Bogus library for realistic test data
- Maintain pattern compatibility with real logs

### 3. Snapshot Testing
- Use Verify.Xunit for snapshot-based testing
- Capture known-good outputs as snapshots
- Version control approved snapshots

## Best Practices

### DO:
- ✅ Always copy sample files before modification
- ✅ Use `SampleDataTestBase` for sample-dependent tests
- ✅ Validate against multiple samples for robustness
- ✅ Create embedded resources for critical test scenarios
- ✅ Document sample dependencies in test comments
- ✅ Use theory data for parametric testing with samples

### DON'T:
- ❌ Modify original sample files directly
- ❌ Hardcode paths to sample directories
- ❌ Assume sample files will always exist
- ❌ Create dependencies between tests through shared files
- ❌ Skip cleanup of temporary test files

## Sample Data Validation Checklist

When using sample data in tests:

1. **Isolation**: Is the test isolated from other tests?
2. **Cleanup**: Are temporary files cleaned up after test?
3. **Resilience**: Will test work when samples are removed?
4. **Documentation**: Is sample usage clearly documented?
5. **Validation**: Are results validated against expectations?

## Future Improvements

### Planned Enhancements:
1. **Automated Embedding**: Script to automatically embed frequently-used samples
2. **Pattern Library**: Extract common patterns from samples into reusable test data
3. **Performance Baselines**: Use samples to establish performance benchmarks
4. **Regression Suite**: Automated regression testing against all samples

### Migration Timeline:
1. **Phase 1**: Current - Use samples with isolation (COMPLETED)
2. **Phase 2**: Create embedded resources for critical tests
3. **Phase 3**: Generate synthetic test data based on patterns
4. **Phase 4**: Remove dependency on sample directories

## Test Execution

### Running Sample-Based Tests:
```bash
# Run all integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run sample validation tests
dotnet test --filter "FullyQualifiedName~SampleOutputValidation"

# Run specific analyzer tests with samples
dotnet test --filter "FullyQualifiedName~SampleDataTests"
```

### Performance Considerations:
- Sample-based tests may be slower due to file I/O
- Consider using `[Trait("Category", "Integration")]` for categorization
- Run fast unit tests separately from sample-based integration tests

## Troubleshooting

### Common Issues:

1. **Sample Not Found**:
   - Verify sample_logs directory exists
   - Check working directory is project root
   - Ensure samples haven't been deleted

2. **Test Isolation Failures**:
   - Check temp directory cleanup
   - Verify no shared state between tests
   - Use unique file names for concurrent tests

3. **Output Validation Mismatches**:
   - Expected outputs may use different formatting
   - Focus on semantic equivalence, not exact matches
   - Consider tolerances for numeric comparisons

## Summary

The test data management system provides:
- **Robust testing** with real-world crash logs
- **Validation** against known-good outputs
- **Isolation** to prevent test interference
- **Migration path** for sample removal
- **Comprehensive coverage** of analysis scenarios

By following these patterns and practices, the Scanner111 test suite maintains high quality while being resilient to future changes in test data availability.