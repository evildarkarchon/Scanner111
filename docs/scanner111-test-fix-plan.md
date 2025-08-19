# Scanner111 Test Suite Fix - Implementation Plan

## Phase 1: Create Abstraction Interfaces (Priority: High)

### 1.1 File System Abstractions
Create these interfaces in `Scanner111.Core/Abstractions/`:

```csharp
// IFileSystem.cs
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void CopyFile(string source, string destination, bool overwrite = false);
    void MoveFile(string source, string destination);
    DateTime GetLastWriteTime(string path);
    long GetFileSize(string path);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    void WriteAllText(string path, string content);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
}

// IFileVersionInfoProvider.cs
public interface IFileVersionInfoProvider
{
    FileVersionInfo GetVersionInfo(string fileName);
    bool TryGetVersionInfo(string fileName, out FileVersionInfo versionInfo);
}

// IEnvironmentPathProvider.cs
public interface IEnvironmentPathProvider
{
    string GetFolderPath(Environment.SpecialFolder folder);
    string GetEnvironmentVariable(string variable);
    string CurrentDirectory { get; }
    string TempPath { get; }
    string UserName { get; }
    string MachineName { get; }
}

// IPathService.cs
public interface IPathService
{
    string Combine(params string[] paths);
    string GetDirectoryName(string path);
    string GetFileName(string path);
    string GetFileNameWithoutExtension(string path);
    string GetExtension(string path);
    string GetFullPath(string path);
    string NormalizePath(string path);
    bool IsPathRooted(string path);
}
```

### 1.2 Implementation Classes
Create in `Scanner111.Core/Infrastructure/`:

```csharp
// FileSystem.cs - Production implementation
public class FileSystem : IFileSystem
{
    // Implement all methods using System.IO
    // Add proper error handling and path normalization
}

// FileVersionInfoProvider.cs
public class FileVersionInfoProvider : IFileVersionInfoProvider
{
    // Implement with try-catch for FileVersionInfo.GetVersionInfo
}

// EnvironmentPathProvider.cs
public class EnvironmentPathProvider : IEnvironmentPathProvider
{
    // Implement using Environment class
}

// PathService.cs
public class PathService : IPathService
{
    public string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        
        // Handle Windows path issues
        path = path.Replace('/', Path.DirectorySeparatorChar);
        path = Environment.ExpandEnvironmentVariables(path);
        
        // Handle paths with spaces
        if (path.Contains(' ') && !path.StartsWith("\""))
        {
            // Only quote if needed for command line usage
            // Keep unquoted for file system operations
        }
        
        return Path.GetFullPath(path);
    }
}
```

## Phase 2: Dependency Injection Registration

### 2.1 Update Service Registration
Modify `Scanner111.Core/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddCoreServices(this IServiceCollection services)
{
    // Add abstraction services
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<IFileVersionInfoProvider, FileVersionInfoProvider>();
    services.AddSingleton<IEnvironmentPathProvider, EnvironmentPathProvider>();
    services.AddSingleton<IPathService, PathService>();
    
    // Existing services...
    return services;
}
```

### 2.2 Update GUI and CLI Registration
- `Scanner111.GUI/App.axaml.cs`: Include core abstractions
- `Scanner111.CLI/Program.cs`: Include core abstractions

## Phase 3: Refactor Existing Components

### 3.1 High-Priority Refactoring Targets

#### GamePathDetection Service
**File**: `Scanner111.Core/Services/GamePathDetection.cs`
```csharp
public class GamePathDetection : IGamePathDetection
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentPathProvider _environment;
    private readonly IPathService _pathService;
    
    public GamePathDetection(
        IFileSystem fileSystem,
        IEnvironmentPathProvider environment,
        IPathService pathService)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _pathService = pathService;
    }
    
    // Refactor all File.Exists, Directory.Exists, Path.Combine calls
}
```

#### CrashLogParser
**File**: `Scanner111.Core/Parsing/CrashLogParser.cs`
```csharp
public class CrashLogParser : ICrashLogParser
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;
    
    // Refactor static methods to instance methods
    // Use dependency injection
}
```

#### Analyzers
Update each analyzer in `Scanner111.Core/Analyzers/`:
- `FormIdAnalyzer.cs`
- `PluginAnalyzer.cs`
- `SuspectScanner.cs`
- `FileIntegrityAnalyzer.cs`

### 3.2 FCX Components
Update `Scanner111.Core/FCX/`:
- `HashValidationService.cs`: Use IFileSystem for file operations
- `BackupService.cs`: Use IFileSystem and IPathService

## Phase 4: Test Infrastructure

### 4.1 Create Test Doubles
In `Scanner111.Tests/TestHelpers/`:

```csharp
// TestFileSystem.cs
public class TestFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _directories = new();
    
    public void AddFile(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = Encoding.UTF8.GetBytes(content);
        
        // Auto-create parent directories
        var dir = Path.GetDirectoryName(normalizedPath);
        while (!string.IsNullOrEmpty(dir))
        {
            _directories.Add(NormalizePath(dir));
            dir = Path.GetDirectoryName(dir);
        }
    }
    
    public bool FileExists(string path) => 
        _files.ContainsKey(NormalizePath(path));
    
    // Implement other methods...
}

// TestEnvironmentPathProvider.cs
public class TestEnvironmentPathProvider : IEnvironmentPathProvider
{
    private readonly Dictionary<string, string> _variables = new();
    private readonly Dictionary<Environment.SpecialFolder, string> _folders = new();
    
    public void SetVariable(string name, string value) =>
        _variables[name] = value;
    
    public void SetSpecialFolder(Environment.SpecialFolder folder, string path) =>
        _folders[folder] = path;
    
    // Implement interface...
}
```

### 4.2 Test Fixture Base Classes
Create `Scanner111.Tests/TestHelpers/TestFixtureBase.cs`:

```csharp
public abstract class TestFixtureBase : IDisposable
{
    protected IServiceProvider ServiceProvider { get; }
    protected TestFileSystem FileSystem { get; }
    protected TestEnvironmentPathProvider Environment { get; }
    protected IPathService PathService { get; }
    
    protected TestFixtureBase()
    {
        var services = new ServiceCollection();
        
        FileSystem = new TestFileSystem();
        Environment = new TestEnvironmentPathProvider();
        PathService = new PathService();
        
        services.AddSingleton<IFileSystem>(FileSystem);
        services.AddSingleton<IEnvironmentPathProvider>(Environment);
        services.AddSingleton<IPathService>(PathService);
        
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }
    
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Override in derived classes to add specific services
    }
    
    public virtual void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}
```

## Phase 5: Fix Specific Test Issues

### 5.1 Path Handling Tests
Create dedicated tests for path normalization:

```csharp
[Fact]
public void PathService_HandlesWindowsPathsWithSpaces()
{
    var pathService = new PathService();
    var path = @"C:\Program Files\Some Game\Data";
    var normalized = pathService.NormalizePath(path);
    
    normalized.Should().Be(@"C:\Program Files\Some Game\Data");
}

[Fact]
public void PathService_ConvertsMixedSeparators()
{
    var pathService = new PathService();
    var path = @"C:/Program Files\Some/Game\Data";
    var normalized = pathService.NormalizePath(path);
    
    normalized.Should().Be(@"C:\Program Files\Some\Game\Data");
}
```

### 5.2 Timeout Issues
Add timeout configuration to test collections:

```csharp
// In Scanner111.Tests/TestHelpers/TestCollections.cs
public class TestTimeoutConfiguration
{
    public const int ShortTimeout = 5000;  // 5 seconds
    public const int MediumTimeout = 10000; // 10 seconds
    public const int LongTimeout = 30000;   // 30 seconds
    
    public static TimeSpan GetTimeout(TestComplexity complexity) => complexity switch
    {
        TestComplexity.Simple => TimeSpan.FromMilliseconds(ShortTimeout),
        TestComplexity.Medium => TimeSpan.FromMilliseconds(MediumTimeout),
        TestComplexity.Complex => TimeSpan.FromMilliseconds(LongTimeout),
        _ => TimeSpan.FromMilliseconds(MediumTimeout)
    };
}

public enum TestComplexity
{
    Simple,
    Medium,
    Complex
}
```

### 5.3 Test Isolation Improvements

#### Create Test Data Builder
```csharp
public class TestDataBuilder
{
    private readonly TestFileSystem _fileSystem;
    
    public TestDataBuilder(TestFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    public TestDataBuilder WithCrashLog(string name, string content)
    {
        _fileSystem.AddFile($"logs/{name}", content);
        return this;
    }
    
    public TestDataBuilder WithGameInstallation(string gamePath)
    {
        _fileSystem.AddDirectory(gamePath);
        _fileSystem.AddFile($"{gamePath}/Fallout4.exe", "dummy");
        return this;
    }
    
    public TestDataBuilder WithModManagerProfile(string profilePath)
    {
        _fileSystem.AddDirectory(profilePath);
        _fileSystem.AddFile($"{profilePath}/modlist.txt", "# Mod list");
        return this;
    }
}
```

## Phase 6: Migration Strategy

### 6.1 Incremental Migration
1. **Week 1**: Implement abstractions and test doubles
2. **Week 2**: Migrate critical path components (GamePathDetection, CrashLogParser)
3. **Week 3**: Migrate analyzers one by one
4. **Week 4**: Update all tests to use test doubles

### 6.2 Test Coverage Goals
- Unit tests: 80% coverage using test doubles
- Integration tests: 20% coverage with real file system (isolated temp directories)
- No test should depend on:
  - User's actual game installations
  - System-specific paths
  - Network resources (unless testing network functionality)

## Phase 7: Validation and Monitoring

### 7.1 Success Metrics
- [ ] All tests pass consistently on Windows
- [ ] Test execution time reduced by 50%
- [ ] No test failures due to path handling
- [ ] Zero dependencies on external system resources in unit tests
- [ ] Test coverage above 80%

### 7.2 Monitoring Tools
- Add test performance tracking
- Implement flaky test detection
- Create test health dashboard

## Implementation Notes for Claude Code

### Priority Order
1. **Critical**: Abstractions (IFileSystem, IPathService)
2. **High**: GamePathDetection, CrashLogParser refactoring
3. **Medium**: Analyzer refactoring
4. **Low**: GUI-specific test improvements

### Key Commands for Testing
```bash
# Run only unit tests (fast)
dotnet test --filter "Category!=Integration&Category!=GUI" 

# Run specific analyzer tests
dotnet test --filter "ClassName=FormIdAnalyzerTests"

# Run with detailed output for debugging
dotnet test -v detailed --filter "ClassName=GamePathDetectionTests"

# Check for test timeouts
dotnet test --blame-hang-timeout 30s
```

### Common Pitfalls to Avoid
1. Don't use `Path.Combine` directly in production code - use `IPathService`
2. Always normalize paths before comparison
3. Use `StringComparison.OrdinalIgnoreCase` for path comparisons on Windows
4. Mock file system operations in unit tests
5. Use real file system only in integration tests with temp directories

This plan provides a systematic approach to fixing the test suite while maintaining backward compatibility and improving overall code quality.