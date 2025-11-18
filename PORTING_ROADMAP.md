# Scanner111 Porting Roadmap

**Version**: 1.0
**Last Updated**: 2025-11-17
**Status**: Foundation Phase - No production code written

This document provides a comprehensive roadmap for porting CLASSIC (Crash Log Auto Scanner & Setup Integrity Checker) from Python/Rust to C#. It is organized by development phases with detailed task breakdowns, C# implementation guidance, and acceptance criteria.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Phase 1: Foundation & Core Models](#phase-1-foundation--core-models)
3. [Phase 2: File I/O & Log Parsing](#phase-2-file-io--log-parsing)
4. [Phase 3: Analysis Components](#phase-3-analysis-components)
5. [Phase 4: Report Composition System](#phase-4-report-composition-system)
6. [Phase 5: Orchestration & Pipeline](#phase-5-orchestration--pipeline)
7. [Phase 6: Configuration & YAML Integration](#phase-6-configuration--yaml-integration)
8. [Phase 7: Database & FormID Analysis](#phase-7-database--formid-analysis)
9. [Phase 8: GUI Integration (Avalonia)](#phase-8-gui-integration-avalonia)
10. [Phase 9: Testing & Validation](#phase-9-testing--validation)
11. [Phase 10: Optimization & Polish](#phase-10-optimization--polish)
12. [Dependencies & Libraries](#dependencies--libraries)
13. [Python to C# Translation Guide](#python-to-c-translation-guide)
14. [Testing Strategy](#testing-strategy)
15. [Milestones & Acceptance Criteria](#milestones--acceptance-criteria)

---

## Architecture Overview

### Original CLASSIC Architecture (Python/Rust)

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLASSIC Architecture                     │
├─────────────────────────────────────────────────────────────────┤
│  Entry Point: CLASSIC_ScanLogs.py (CLI) / CLASSIC_Interface.py │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ScanLogsExecutor (Orchestration)                         │  │
│  │  - Manages scan lifecycle                                │  │
│  │  - Concurrency control (50 tasks)                        │  │
│  │  - Progress tracking & statistics                        │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           ↓                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ OrchestratorCore (Analysis Pipeline)                     │  │
│  │  - Process individual crash logs                         │  │
│  │  - Coordinate analyzers & scanners                       │  │
│  │  - Generate report fragments                             │  │
│  │  - Async FormID database lookups                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│         ↓            ↓           ↓            ↓                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │  Parser  │  │ Plugin   │  │ Suspect  │  │ FormID   │       │
│  │ (150x 🦀)│  │Analyzer  │  │ Scanner  │  │ Analyzer │       │
│  │          │  │ (30x 🦀) │  │          │  │          │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│         ↓            ↓           ↓            ↓                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ReportComposer (Fragment Composition)                    │  │
│  │  - Immutable report building                             │  │
│  │  - Conditional sections                                  │  │
│  │  - Markdown generation                                   │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           ↓                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ File I/O (Async, 10x Rust acceleration)                  │  │
│  │  - Write {crashlog}-AUTOSCAN.md reports                  │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘

🦀 = Rust-accelerated (with Python fallback)
```

### Scanner111 Target Architecture (C#/Avalonia)

```
┌─────────────────────────────────────────────────────────────────┐
│                      Scanner111 Architecture                     │
├─────────────────────────────────────────────────────────────────┤
│  Scanner111 (GUI - Avalonia MVVM)                               │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ MainWindow (View)                                        │  │
│  │  ↕ (Data Binding)                                        │  │
│  │ MainWindowViewModel (ViewModel)                          │  │
│  │  - ScanCommand (ReactiveCommand)                         │  │
│  │  - Progress observables                                  │  │
│  │  - Statistics binding                                    │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           ↓ (Calls)                              │
├─────────────────────────────────────────────────────────────────┤
│  Scanner111.Common (Business Logic - Framework Agnostic)        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ScanExecutor (Orchestration)                             │  │
│  │  - async Task ExecuteScanAsync(ScanConfig, IProgress)    │  │
│  │  - Concurrency with SemaphoreSlim                        │  │
│  │  - Statistics tracking                                   │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           ↓                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ LogOrchestrator (Analysis Pipeline)                      │  │
│  │  - async Task<LogAnalysisResult> ProcessLogAsync(...)    │  │
│  │  - Coordinate analyzers                                  │  │
│  │  - Build report fragments                                │  │
│  └──────────────────────────────────────────────────────────┘  │
│         ↓            ↓           ↓            ↓                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ Log      │  │ Plugin   │  │ Suspect  │  │ FormID   │       │
│  │ Parser   │  │ Analyzer │  │ Scanner  │  │ Analyzer │       │
│  │ (regex)  │  │ (regex)  │  │ (pattern)│  │ (DB)     │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│         ↓            ↓           ↓            ↓                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ReportBuilder (Fragment Composition)                     │  │
│  │  - Immutable records with init                           │  │
│  │  - Builder pattern with fluent API                       │  │
│  │  - Conditional sections via LINQ                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           ↓                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ File I/O (Async with FileStream)                         │  │
│  │  - async Task WriteReportAsync(...)                      │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Foundation & Core Models

**Goal**: Establish foundational data structures and configuration models
**Dependencies**: None
**Estimated Effort**: 1-2 weeks

### 1.1 Core Configuration Models

**Location**: `Scanner111.Common/Models/Configuration/`

#### Tasks

- [x] **ScanConfig.cs** - Scan configuration settings
  ```csharp
  namespace Scanner111.Common.Models.Configuration;

  /// <summary>
  /// Configuration for crash log scanning operations.
  /// </summary>
  public record ScanConfig
  {
      public bool FcxMode { get; init; }
      public bool ShowFormIdValues { get; init; }
      public bool MoveUnsolvedLogs { get; init; }
      public bool SimplifyLogs { get; init; }
      public IReadOnlyDictionary<string, string> CustomPaths { get; init; } = new Dictionary<string, string>();
      public int MaxConcurrent { get; init; } = 50;
      public bool FormIdDatabaseExists { get; init; }
      public IReadOnlyList<string> RemoveList { get; init; } = Array.Empty<string>();
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/models/scan_config.py`

- [x] **ScanStatistics.cs** - Scan progress and results tracking
  ```csharp
  public record ScanStatistics
  {
      public int Scanned { get; init; }
      public int Incomplete { get; init; }
      public int Failed { get; init; }
      public int TotalFiles { get; init; }
      public DateTime ScanStartTime { get; init; }
      public TimeSpan ElapsedTime => DateTime.UtcNow - ScanStartTime;
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/models/scan_statistics.py`

- [x] **ScanResult.cs** - Overall scan results
  ```csharp
  public record ScanResult
  {
      public ScanStatistics Statistics { get; init; } = null!;
      public IReadOnlyList<string> FailedLogs { get; init; } = Array.Empty<string>();
      public TimeSpan ScanDuration { get; init; }
      public IReadOnlyList<string> ProcessedFiles { get; init; } = Array.Empty<string>();
      public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/models/scan_result.py`

### 1.2 Log Analysis Models

**Location**: `Scanner111.Common/Models/Analysis/`

#### Tasks

- [x] **LogSegment.cs** - Represents parsed log sections
  ```csharp
  public record LogSegment
  {
      public string Name { get; init; } = string.Empty;
      public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
      public int StartIndex { get; init; }
      public int EndIndex { get; init; }
  }
  ```

- [x] **CrashHeader.cs** - Crash log header information
  ```csharp
  public record CrashHeader
  {
      public string GameVersion { get; init; } = string.Empty;
      public string CrashGeneratorVersion { get; init; } = string.Empty;
      public string MainError { get; init; } = string.Empty;
      public DateTime? CrashTimestamp { get; init; }
  }
  ```

- [x] **PluginInfo.cs** - Game plugin information
  ```csharp
  public record PluginInfo
  {
      public string FormIdPrefix { get; init; } = string.Empty; // e.g., "E7" or "FE:000"
      public string PluginName { get; init; } = string.Empty;
      public bool IsLightPlugin => FormIdPrefix.StartsWith("FE:");

      // Load order is implicit - position in the plugin list
  }
  ```

- [x] **ModuleInfo.cs** - DLL module information
  ```csharp
  public record ModuleInfo
  {
      public string Name { get; init; } = string.Empty;
      public string? Version { get; init; }
      public string? Path { get; init; }
  }
  ```

### 1.3 Report Models

**Location**: `Scanner111.Common/Models/Reporting/`

#### Tasks

- [x] **ReportFragment.cs** - Immutable report section
  ```csharp
  /// <summary>
  /// Immutable report fragment for functional composition.
  /// </summary>
  public record ReportFragment
  {
      public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
      public bool HasContent => Lines.Count > 0;

      public static ReportFragment FromLines(params string[] lines)
          => new() { Lines = lines.ToList() };

      public ReportFragment WithHeader(string header)
      {
          if (!HasContent) return this;
          return this with { Lines = new[] { header, "" }.Concat(Lines).ToList() };
      }

      public static ReportFragment operator +(ReportFragment a, ReportFragment b)
          => new() { Lines = a.Lines.Concat(b.Lines).ToList() };
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/fragments/report_fragment.py`

- [x] **LogAnalysisResult.cs** - Complete analysis result
  ```csharp
  public record LogAnalysisResult
  {
      public string LogFileName { get; init; } = string.Empty;
      public CrashHeader Header { get; init; } = null!;
      public IReadOnlyList<LogSegment> Segments { get; init; } = Array.Empty<LogSegment>();
      public ReportFragment Report { get; init; } = null!;
      public bool IsComplete { get; init; } // Has plugins section
      public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
  }
  ```

### 1.4 Testing

**Location**: `Scanner111.Common.Tests/Models/`

#### Tasks

- [x] **ConfigurationModelTests.cs** - Test ScanConfig, ScanStatistics, ScanResult
- [x] **AnalysisModelTests.cs** - Test LogSegment, CrashHeader, PluginInfo
- [x] **ReportFragmentTests.cs** - Test immutable composition, operators
  ```csharp
  [Fact]
  public void ReportFragment_WithHeader_AddsHeaderWhenContentExists()
  {
      var fragment = ReportFragment.FromLines("Line 1", "Line 2");
      var result = fragment.WithHeader("HEADER");

      result.Lines.Should().HaveCount(4);
      result.Lines[0].Should().Be("HEADER");
      result.Lines[1].Should().BeEmpty();
  }

  [Fact]
  public void ReportFragment_Addition_CombinesFragments()
  {
      var frag1 = ReportFragment.FromLines("A", "B");
      var frag2 = ReportFragment.FromLines("C", "D");
      var result = frag1 + frag2;

      result.Lines.Should().Equal("A", "B", "C", "D");
  }
  ```

**Acceptance Criteria**:
- ✅ All models are immutable records
- ✅ All properties use init-only setters
- ✅ Collections use IReadOnly* interfaces
- ✅ Complete XML documentation
- ✅ 100% test coverage on models
- ✅ ReportFragment composition matches Python behavior

---

## Phase 2: File I/O & Log Parsing

**Goal**: Implement crash log reading and segment extraction
**Dependencies**: Phase 1 (Core Models)
**Estimated Effort**: 2-3 weeks

### 2.1 Async File I/O

**Location**: `Scanner111.Common/Services/FileIO/`

#### Tasks

- [ ] **IFileIOService.cs** - File I/O abstraction
  ```csharp
  public interface IFileIOService
  {
      Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);
      Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);
      Task<bool> FileExistsAsync(string path);
  }
  ```

- [ ] **FileIOService.cs** - Implementation with UTF-8 + error handling
  ```csharp
  public class FileIOService : IFileIOService
  {
      private static readonly Encoding Utf8WithErrorHandling =
          new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

      public async Task<string> ReadFileAsync(string path, CancellationToken ct = default)
      {
          await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
              FileShare.Read, bufferSize: 4096, useAsync: true);
          using var reader = new StreamReader(stream, Utf8WithErrorHandling);
          return await reader.ReadToEndAsync(ct);
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/FileIO/FileIOCore.py`

- [ ] **FileIOServiceTests.cs** - Test with sample logs
  ```csharp
  [Theory]
  [InlineData("sample_logs/FO4/crash-12624.log")]
  [InlineData("sample_logs/Skyrim/crash-2023-12-07-02-24-27.log")]
  public async Task ReadFileAsync_WithSampleLogs_Succeeds(string logPath)
  {
      var service = new FileIOService();
      var content = await service.ReadFileAsync(logPath);

      content.Should().NotBeEmpty();
      content.Should().Contain("SYSTEM SPECS"); // Common section
  }
  ```

### 2.2 Log Parsing Core

**Location**: `Scanner111.Common/Services/Parsing/`

#### Tasks

- [ ] **ILogParser.cs** - Parser interface
  ```csharp
  public interface ILogParser
  {
      Task<LogParseResult> ParseAsync(string logContent, CancellationToken ct = default);
      CrashHeader? ParseHeader(string logContent);
      IReadOnlyList<LogSegment> ExtractSegments(string logContent);
  }

  public record LogParseResult
  {
      public CrashHeader Header { get; init; } = null!;
      public IReadOnlyList<LogSegment> Segments { get; init; } = Array.Empty<LogSegment>();
      public bool IsValid { get; init; }
      public string? ErrorMessage { get; init; }
  }
  ```

- [ ] **LogParser.cs** - Main parser implementation
  ```csharp
  public class LogParser : ILogParser
  {
      private static readonly Regex SegmentHeaderRegex = new(
          @"^\s*\[(.*?)\]|^SYSTEM SPECS:|^PROBABLE CALL STACK:|^MODULES:|^PLUGINS:",
          RegexOptions.Compiled | RegexOptions.Multiline
      );

      public IReadOnlyList<LogSegment> ExtractSegments(string logContent)
      {
          var segments = new List<LogSegment>();
          var matches = SegmentHeaderRegex.Matches(logContent);

          for (int i = 0; i < matches.Count; i++)
          {
              var startMatch = matches[i];
              var endIndex = i < matches.Count - 1
                  ? matches[i + 1].Index
                  : logContent.Length;

              var sectionContent = logContent[startMatch.Index..endIndex];
              var lines = sectionContent.Split('\n', StringSplitOptions.TrimEntries);

              segments.Add(new LogSegment
              {
                  Name = startMatch.Groups[1].Value.Trim(),
                  Lines = lines,
                  StartIndex = startMatch.Index,
                  EndIndex = endIndex
              });
          }

          return segments;
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/Parser.py:find_segments()`

- [ ] **CrashHeaderParser.cs** - Parse crash header metadata
  ```csharp
  public class CrashHeaderParser
  {
      private static readonly Regex GameVersionRegex = new(
          @"Fallout 4 v([\d.]+)|Skyrim SE v([\d.]+)",
          RegexOptions.Compiled
      );

      private static readonly Regex MainErrorRegex = new(
          @"Unhandled exception ""(.+?)""",
          RegexOptions.Compiled
      );

      public CrashHeader? Parse(string logContent)
      {
          // Extract version, error, timestamp
          // Reference: parse_crash_header() in Parser.py
      }
  }
  ```

- [ ] **LogReformatter.cs** - Pre-process Buffout 4 logs
  ```csharp
  /// <summary>
  /// Reformats Buffout 4 crash logs by removing extra spaces in load order.
  /// </summary>
  public static class LogReformatter
  {
      private static readonly Regex LoadOrderSpacesRegex = new(
          @"^\s*(\d+)\s+(\d+)\s+([A-Fa-f0-9]+)\s+(.+?)$",
          RegexOptions.Compiled | RegexOptions.Multiline
      );

      public static string ReformatBuffout4LoadOrder(string logContent)
      {
          // Remove extra spaces between load order columns
          // Reference: Code_To_Port/ClassicLib/ScanLog/Parser.py (inline reformatting)
      }
  }
  ```

### 2.3 Testing with Sample Logs

**Location**: `Scanner111.Common.Tests/Services/Parsing/`

#### Tasks

- [ ] **LogParserTests.cs** - Comprehensive parser tests
  ```csharp
  public class LogParserTests
  {
      [Theory]
      [InlineData("sample_logs/FO4/crash-12624.log", 6)] // Expected: 6 segments
      [InlineData("sample_logs/Skyrim/crash-2023-12-07-02-24-27.log", 5)]
      public async Task ParseAsync_WithSampleLogs_ExtractsCorrectSegmentCount(
          string logPath, int expectedSegments)
      {
          var parser = new LogParser();
          var content = await File.ReadAllTextAsync(logPath);

          var result = await parser.ParseAsync(content);

          result.Segments.Should().HaveCount(expectedSegments);
      }

      [Fact]
      public async Task ParseAsync_WithIncompleteLog_MarksAsInvalid()
      {
          var incompleteLog = "Unhandled exception\nSYSTEM SPECS:\n...";
          var parser = new LogParser();

          var result = await parser.ParseAsync(incompleteLog);

          result.IsValid.Should().BeFalse();
          result.ErrorMessage.Should().Contain("incomplete");
      }
  }
  ```

- [ ] **CrashHeaderParserTests.cs** - Header parsing tests
- [ ] **LogReformatterTests.cs** - Test Buffout 4 reformatting

**Acceptance Criteria**:
- ✅ Parses all 1,013 FO4 sample logs without exceptions
- ✅ Parses all 299 Skyrim sample logs without exceptions
- ✅ Correctly identifies 6 major segments (Compatibility, System Specs, Call Stack, Modules, XSE Plugins, Plugins)
- ✅ Handles incomplete/partial logs gracefully
- ✅ Extracts crash header metadata (game version, crash generator version, main error)
- ✅ UTF-8 encoding with error handling for malformed logs
- ✅ 90%+ test coverage

---

## Phase 3: Analysis Components

**Goal**: Implement core analysis logic for plugins, suspects, settings
**Dependencies**: Phase 1, Phase 2
**Estimated Effort**: 3-4 weeks

### 3.1 Plugin Analysis

**Location**: `Scanner111.Common/Services/Analysis/`

#### Tasks

- [ ] **IPluginAnalyzer.cs** - Plugin analysis interface
  ```csharp
  public interface IPluginAnalyzer
  {
      Task<PluginAnalysisResult> AnalyzeAsync(
          IReadOnlyList<LogSegment> segments,
          CancellationToken ct = default);

      IReadOnlyList<PluginInfo> ExtractPlugins(LogSegment pluginSegment);
      IReadOnlyList<string> MatchPluginPatterns(
          IReadOnlyList<string> pluginNames,
          IReadOnlyList<string> patterns);
  }
  ```

- [ ] **PluginAnalyzer.cs** - Plugin detection and matching
  ```csharp
  public class PluginAnalyzer : IPluginAnalyzer
  {
      private readonly Dictionary<string, Regex> _compiledPatterns = new();

      public IReadOnlyList<string> MatchPluginPatterns(
          IReadOnlyList<string> pluginNames,
          IReadOnlyList<string> patterns)
      {
          var matches = new List<string>();

          foreach (var pattern in patterns)
          {
              var regex = GetOrCompileRegex(pattern);
              matches.AddRange(pluginNames.Where(p => regex.IsMatch(p)));
          }

          return matches;
      }

      private Regex GetOrCompileRegex(string pattern)
      {
          if (!_compiledPatterns.TryGetValue(pattern, out var regex))
          {
              regex = new Regex(pattern,
                  RegexOptions.Compiled | RegexOptions.IgnoreCase);
              _compiledPatterns[pattern] = regex;
          }
          return regex;
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/PluginAnalyzer.py`

- [ ] **PluginListParser.cs** - Parse plugin list (which IS the load order)
  ```csharp
  public class PluginListParser
  {
      private static readonly Regex PluginLineRegex = new(
          @"^\s*\[([A-Fa-f0-9:]+)\]\s+(.+?)\s*$",
          RegexOptions.Compiled | RegexOptions.Multiline
      );

      public IReadOnlyList<PluginInfo> ParsePluginList(LogSegment pluginSegment)
      {
          // Format: [FormIdPrefix] PluginName.ext
          // Example: [E7] StartMeUp.esp
          // Example: [FE:000] PPF.esm

          var plugins = new List<PluginInfo>();

          foreach (var line in pluginSegment.Lines)
          {
              var match = PluginLineRegex.Match(line);
              if (match.Success)
              {
                  plugins.Add(new PluginInfo
                  {
                      FormIdPrefix = match.Groups[1].Value, // "E7" or "FE:000"
                      PluginName = match.Groups[2].Value    // "StartMeUp.esp"
                  });
              }
          }

          return plugins;
          // Note: Load order is the position in this list (index 0 = first loaded)
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/PluginAnalyzer.py:loadorder_scan_log()`

- [ ] **PluginLimitChecker.cs** - Plugin count validation
  ```csharp
  public class PluginLimitChecker
  {
      private const int MaxFullPlugins = 254;
      private const int MaxLightPlugins = 4096;

      public ValidationResult CheckLimits(IReadOnlyList<PluginInfo> plugins)
      {
          var fullPlugins = plugins.Count(p => !p.IsLightPlugin);
          var lightPlugins = plugins.Count(p => p.IsLightPlugin);

          // Return warnings if approaching limits
      }
  }
  ```

### 3.2 Suspect Detection

**Location**: `Scanner111.Common/Services/Analysis/`

#### Tasks

- [ ] **ISuspectScanner.cs** - Suspect detection interface
  ```csharp
  public interface ISuspectScanner
  {
      Task<SuspectScanResult> ScanAsync(
          CrashHeader header,
          IReadOnlyList<LogSegment> segments,
          SuspectPatterns patterns,
          CancellationToken ct = default);
  }

  public record SuspectScanResult
  {
      public IReadOnlyList<string> ErrorMatches { get; init; } = Array.Empty<string>();
      public IReadOnlyList<string> StackMatches { get; init; } = Array.Empty<string>();
      public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
  }
  ```

- [ ] **SuspectScanner.cs** - Pattern matching implementation
  ```csharp
  public class SuspectScanner : ISuspectScanner
  {
      public async Task<SuspectScanResult> ScanAsync(
          CrashHeader header,
          IReadOnlyList<LogSegment> segments,
          SuspectPatterns patterns,
          CancellationToken ct = default)
      {
          var errorMatches = await ScanMainErrorAsync(header.MainError, patterns.ErrorPatterns, ct);
          var stackMatches = await ScanCallStackAsync(segments, patterns.StackSignatures, ct);

          return new SuspectScanResult
          {
              ErrorMatches = errorMatches,
              StackMatches = stackMatches,
              Recommendations = GenerateRecommendations(errorMatches, stackMatches)
          };
      }

      private Task<IReadOnlyList<string>> ScanMainErrorAsync(
          string mainError,
          IReadOnlyList<SuspectPattern> patterns,
          CancellationToken ct)
      {
          // Match error message against known patterns
          // Reference: suspect_scan_mainerror() in SuspectScanner.py
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/SuspectScanner.py`

- [ ] **SuspectPattern.cs** - Pattern definition model
  ```csharp
  public record SuspectPattern
  {
      public string Pattern { get; init; } = string.Empty;
      public string Category { get; init; } = string.Empty; // "error" or "stack"
      public string Message { get; init; } = string.Empty;
      public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
  }

  public record SuspectPatterns
  {
      public IReadOnlyList<SuspectPattern> ErrorPatterns { get; init; } = Array.Empty<SuspectPattern>();
      public IReadOnlyList<SuspectPattern> StackSignatures { get; init; } = Array.Empty<SuspectPattern>();
  }
  ```

### 3.3 Settings Validation

**Location**: `Scanner111.Common/Services/Analysis/`

#### Tasks

- [ ] **ISettingsScanner.cs** - Settings validation interface
  ```csharp
  public interface ISettingsScanner
  {
      Task<SettingsScanResult> ScanAsync(
          LogSegment compatibilitySegment,
          GameSettings expectedSettings,
          CancellationToken ct = default);
  }
  ```

- [ ] **SettingsScanner.cs** - Buffout 4 / Crash Logger settings validation
  ```csharp
  public class SettingsScanner : ISettingsScanner
  {
      public Task<SettingsScanResult> ScanAsync(
          LogSegment compatibilitySegment,
          GameSettings expectedSettings,
          CancellationToken ct)
      {
          // Check Buffout 4 settings (MemoryManager, AutoScanning, etc.)
          // Reference: scan_buffout_memorymanagement_settings() in SettingsScanner.py
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/SettingsScanner.py`

- [ ] **GameSettings.cs** - Expected settings model
  ```csharp
  public record GameSettings
  {
      public string GameName { get; init; } = string.Empty;
      public Dictionary<string, string> RecommendedSettings { get; init; } = new();
      public string LatestCrashLoggerVersion { get; init; } = string.Empty;
  }
  ```

### 3.4 Testing

**Location**: `Scanner111.Common.Tests/Services/Analysis/`

#### Tasks

- [ ] **PluginAnalyzerTests.cs**
  ```csharp
  [Fact]
  public void ParsePluginList_WithValidFormat_ExtractsPluginsCorrectly()
  {
      var pluginText = @"
          [E7]     StartMeUp.esp
          [E8]     PlayerComments.esp
          [FE:000] PPF.esm
          [FE:001] Resources Expanded - Recipes.esl";

      var segment = new LogSegment { Lines = pluginText.Split('\n') };
      var parser = new PluginListParser();

      var plugins = parser.ParsePluginList(segment);

      plugins.Should().HaveCount(4);
      plugins[0].FormIdPrefix.Should().Be("E7");
      plugins[0].PluginName.Should().Be("StartMeUp.esp");
      plugins[0].IsLightPlugin.Should().BeFalse();

      plugins[2].FormIdPrefix.Should().Be("FE:000");
      plugins[2].PluginName.Should().Be("PPF.esm");
      plugins[2].IsLightPlugin.Should().BeTrue();
  }

  [Theory]
  [InlineData(new[] { "TestMod.esp", "AnotherMod.esm" }, new[] { "Test.*" }, 1)]
  [InlineData(new[] { "ModA.esp", "ModB.esp" }, new[] { "Mod[AB].*" }, 2)]
  public void MatchPluginPatterns_WithRegex_MatchesCorrectly(
      string[] plugins, string[] patterns, int expectedMatches)
  {
      var analyzer = new PluginAnalyzer();
      var matches = analyzer.MatchPluginPatterns(plugins, patterns);
      matches.Should().HaveCount(expectedMatches);
  }
  ```

- [ ] **SuspectScannerTests.cs** - Test with known error patterns
- [ ] **SettingsScannerTests.cs** - Test settings detection

**Acceptance Criteria**:
- ✅ Plugin regex matching performs efficiently (compiled regex caching)
- ✅ Plugin list parsing extracts FormID prefix and plugin name correctly
- ✅ Load order is correctly determined by position in plugin list
- ✅ Light plugins (FE:xxx format) correctly identified
- ✅ Plugin limit checks correctly identify approaching limits (254 regular, 4096 light)
- ✅ Suspect scanner detects all ~50 error patterns and ~50 stack signatures
- ✅ Settings scanner validates Buffout 4 configuration
- ✅ 85%+ test coverage

---

## Phase 4: Report Composition System

**Goal**: Implement immutable report building with conditional sections
**Dependencies**: Phase 1
**Estimated Effort**: 1-2 weeks

### 4.1 Report Builder

**Location**: `Scanner111.Common/Services/Reporting/`

#### Tasks

- [ ] **IReportBuilder.cs** - Report composition interface
  ```csharp
  public interface IReportBuilder
  {
      IReportBuilder Add(ReportFragment fragment);
      IReportBuilder AddConditional(Func<ReportFragment> contentGenerator, string? header = null);
      IReportBuilder AddSection(string sectionName, IEnumerable<string> lines);
      ReportFragment Build();
  }
  ```

- [ ] **ReportBuilder.cs** - Fluent builder implementation
  ```csharp
  public class ReportBuilder : IReportBuilder
  {
      private readonly List<ReportFragment> _fragments = new();

      public IReportBuilder Add(ReportFragment fragment)
      {
          if (fragment.HasContent)
              _fragments.Add(fragment);
          return this;
      }

      public IReportBuilder AddConditional(
          Func<ReportFragment> contentGenerator,
          string? header = null)
      {
          var fragment = contentGenerator();
          if (fragment.HasContent)
          {
              var withHeader = header != null
                  ? fragment.WithHeader(header)
                  : fragment;
              _fragments.Add(withHeader);
          }
          return this;
      }

      public ReportFragment Build()
      {
          return _fragments.Aggregate((a, b) => a + b);
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/composition/report_composer.py`

- [ ] **ReportSections.cs** - Standard section generators
  ```csharp
  public static class ReportSections
  {
      public static ReportFragment CreateHeader(CrashHeader header, string gameName)
      {
          return ReportFragment.FromLines(
              $"# Crash Log Analysis - {gameName}",
              $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
              "",
              $"**Game Version**: {header.GameVersion}",
              $"**Crash Generator**: {header.CrashGeneratorVersion}",
              $"**Error**: {header.MainError}",
              ""
          );
      }

      public static ReportFragment CreatePluginSection(
          IReadOnlyList<PluginInfo> plugins)
      {
          // Generate plugin section
          // Reference: generate_plugins_section() in ReportGenerator.py
      }
  }
  ```

### 4.2 Markdown Generation

**Location**: `Scanner111.Common/Services/Reporting/`

#### Tasks

- [ ] **MarkdownFormatter.cs** - Markdown formatting utilities
  ```csharp
  public static class MarkdownFormatter
  {
      public static string Bold(string text) => $"**{text}**";
      public static string Italic(string text) => $"*{text}*";
      public static string Code(string text) => $"`{text}`";
      public static string CodeBlock(string text, string language = "")
          => $"```{language}\n{text}\n```";

      public static string BulletList(IEnumerable<string> items)
          => string.Join("\n", items.Select(i => $"- {i}"));

      public static string NumberedList(IEnumerable<string> items)
          => string.Join("\n", items.Select((i, idx) => $"{idx + 1}. {i}"));
  }
  ```

- [ ] **ReportWriter.cs** - Async file writing
  ```csharp
  public class ReportWriter
  {
      private readonly IFileIOService _fileIO;

      public async Task WriteReportAsync(
          string crashLogPath,
          ReportFragment report,
          CancellationToken ct = default)
      {
          var reportPath = GetReportPath(crashLogPath);
          var content = string.Join("\n", report.Lines);
          await _fileIO.WriteFileAsync(reportPath, content, ct);
      }

      private static string GetReportPath(string crashLogPath)
      {
          // Convert "crash-12624.log" → "crash-12624-AUTOSCAN.md"
          var directory = Path.GetDirectoryName(crashLogPath) ?? string.Empty;
          var fileNameWithoutExt = Path.GetFileNameWithoutExtension(crashLogPath);
          return Path.Combine(directory, $"{fileNameWithoutExt}-AUTOSCAN.md");
      }
  }
  ```

### 4.3 Testing

**Location**: `Scanner111.Common.Tests/Services/Reporting/`

#### Tasks

- [ ] **ReportBuilderTests.cs**
  ```csharp
  [Fact]
  public void Build_WithMultipleFragments_CombinesCorrectly()
  {
      var builder = new ReportBuilder();
      var result = builder
          .Add(ReportFragment.FromLines("Section 1"))
          .Add(ReportFragment.FromLines("Section 2"))
          .Build();

      result.Lines.Should().Equal("Section 1", "Section 2");
  }

  [Fact]
  public void AddConditional_WithEmptyFragment_DoesNotAdd()
  {
      var builder = new ReportBuilder();
      var result = builder
          .AddConditional(() => new ReportFragment(), "HEADER")
          .Build();

      result.HasContent.Should().BeFalse();
  }
  ```

- [ ] **MarkdownFormatterTests.cs** - Test markdown utilities
- [ ] **ReportWriterTests.cs** - Test file writing

**Acceptance Criteria**:
- ✅ Builder supports fluent API
- ✅ Conditional sections only added when content exists
- ✅ Immutable composition (no mutation of fragments)
- ✅ Markdown formatting produces valid markdown
- ✅ Report files written to correct location ({crashlog}-AUTOSCAN.md)
- ✅ 95%+ test coverage

---

## Phase 5: Orchestration & Pipeline

**Goal**: Coordinate analysis pipeline and manage concurrent execution
**Dependencies**: Phases 2, 3, 4
**Estimated Effort**: 2-3 weeks

### 5.1 Log Orchestrator

**Location**: `Scanner111.Common/Services/Orchestration/`

#### Tasks

- [ ] **ILogOrchestrator.cs** - Orchestration interface
  ```csharp
  public interface ILogOrchestrator
  {
      Task<LogAnalysisResult> ProcessLogAsync(
          string logFilePath,
          ScanConfig config,
          CancellationToken ct = default);
  }
  ```

- [ ] **LogOrchestrator.cs** - Main orchestration logic
  ```csharp
  public class LogOrchestrator : ILogOrchestrator
  {
      private readonly ILogParser _parser;
      private readonly IPluginAnalyzer _pluginAnalyzer;
      private readonly ISuspectScanner _suspectScanner;
      private readonly ISettingsScanner _settingsScanner;
      private readonly IReportBuilder _reportBuilder;

      public async Task<LogAnalysisResult> ProcessLogAsync(
          string logFilePath,
          ScanConfig config,
          CancellationToken ct = default)
      {
          // 1. Read log file
          var content = await _fileIO.ReadFileAsync(logFilePath, ct);

          // 2. Parse into segments
          var parseResult = await _parser.ParseAsync(content, ct);

          // 3. Run analysis components in parallel
          var (pluginResult, suspectResult, settingsResult) =
              await RunAnalysisAsync(parseResult, config, ct);

          // 4. Build report
          var report = BuildReport(parseResult, pluginResult, suspectResult, settingsResult);

          // 5. Write report file
          await WriteReportAsync(logFilePath, report, ct);

          return new LogAnalysisResult
          {
              LogFileName = Path.GetFileName(logFilePath),
              Header = parseResult.Header,
              Segments = parseResult.Segments,
              Report = report,
              IsComplete = parseResult.Segments.Any(s => s.Name == "PLUGINS")
          };
      }

      private async Task<(PluginAnalysisResult, SuspectScanResult, SettingsScanResult)>
          RunAnalysisAsync(LogParseResult parseResult, ScanConfig config, CancellationToken ct)
      {
          // Run analyzers in parallel
          var pluginTask = _pluginAnalyzer.AnalyzeAsync(parseResult.Segments, ct);
          var suspectTask = _suspectScanner.ScanAsync(
              parseResult.Header, parseResult.Segments, _patterns, ct);
          var settingsTask = _settingsScanner.ScanAsync(
              parseResult.Segments.First(s => s.Name == "Compatibility"), _gameSettings, ct);

          await Task.WhenAll(pluginTask, suspectTask, settingsTask);

          return (await pluginTask, await suspectTask, await settingsTask);
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/OrchestratorCore.py`

### 5.2 Scan Executor

**Location**: `Scanner111.Common/Services/Orchestration/`

#### Tasks

- [ ] **IScanExecutor.cs** - Scan execution interface
  ```csharp
  public interface IScanExecutor
  {
      Task<ScanResult> ExecuteScanAsync(
          ScanConfig config,
          IProgress<ScanProgress>? progress = null,
          CancellationToken ct = default);
  }

  public record ScanProgress
  {
      public int FilesProcessed { get; init; }
      public int TotalFiles { get; init; }
      public string? CurrentFile { get; init; }
      public ScanStatistics Statistics { get; init; } = null!;
  }
  ```

- [ ] **ScanExecutor.cs** - Concurrent scan execution
  ```csharp
  public class ScanExecutor : IScanExecutor
  {
      private readonly ILogOrchestrator _orchestrator;
      private readonly SemaphoreSlim _concurrencySemaphore;

      public ScanExecutor(ILogOrchestrator orchestrator, int maxConcurrent = 50)
      {
          _orchestrator = orchestrator;
          _concurrencySemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
      }

      public async Task<ScanResult> ExecuteScanAsync(
          ScanConfig config,
          IProgress<ScanProgress>? progress = null,
          CancellationToken ct = default)
      {
          var logFiles = DiscoverCrashLogs(config);
          var statistics = new ScanStatistics
          {
              TotalFiles = logFiles.Count,
              ScanStartTime = DateTime.UtcNow
          };

          var tasks = logFiles.Select(file => ProcessWithSemaphoreAsync(file, config, ct));
          var results = await Task.WhenAll(tasks);

          // Update statistics from results
          // Report progress
          // Return ScanResult
      }

      private async Task<LogAnalysisResult?> ProcessWithSemaphoreAsync(
          string logFile,
          ScanConfig config,
          CancellationToken ct)
      {
          await _concurrencySemaphore.WaitAsync(ct);
          try
          {
              return await _orchestrator.ProcessLogAsync(logFile, config, ct);
          }
          finally
          {
              _concurrencySemaphore.Release();
          }
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/ScanLogsExecutor.py`

### 5.3 Testing

**Location**: `Scanner111.Common.Tests/Services/Orchestration/`

#### Tasks

- [ ] **LogOrchestratorTests.cs**
  ```csharp
  [Theory]
  [InlineData("sample_logs/FO4/crash-12624.log")]
  public async Task ProcessLogAsync_WithSampleLog_GeneratesValidReport(string logPath)
  {
      var orchestrator = CreateOrchestrator();
      var config = new ScanConfig();

      var result = await orchestrator.ProcessLogAsync(logPath, config);

      result.Should().NotBeNull();
      result.Report.HasContent.Should().BeTrue();
      result.IsComplete.Should().BeTrue();
  }
  ```

- [ ] **ScanExecutorTests.cs** - Test concurrent execution
  ```csharp
  [Fact]
  public async Task ExecuteScanAsync_WithMultipleLogs_ProcessesConcurrently()
  {
      var executor = new ScanExecutor(mockOrchestrator, maxConcurrent: 5);
      var config = new ScanConfig();

      var sw = Stopwatch.StartNew();
      var result = await executor.ExecuteScanAsync(config);
      sw.Stop();

      // Should complete faster than sequential
      result.Statistics.Scanned.Should().BeGreaterThan(0);
  }
  ```

**Acceptance Criteria**:
- ✅ Orchestrator coordinates all analysis components
- ✅ Analysis runs in parallel where possible (Task.WhenAll)
- ✅ Executor respects concurrency limits (SemaphoreSlim)
- ✅ Progress reporting works correctly
- ✅ Graceful error handling (failed logs don't crash entire scan)
- ✅ 80%+ test coverage

---

## Phase 6: Configuration & YAML Integration

**Goal**: Load and cache YAML configuration files
**Dependencies**: Phase 1
**Estimated Effort**: 1-2 weeks

### 6.1 YAML Loading

**Location**: `Scanner111.Common/Services/Configuration/`

#### Tasks

- [ ] **Choose YAML Library** - Evaluate options:
  - **YamlDotNet** (Most popular, mature, actively maintained)
  - **SharpYaml** (Good performance)
  - **Recommendation**: YamlDotNet for compatibility and community support

- [ ] **IYamlConfigLoader.cs** - YAML loading interface
  ```csharp
  public interface IYamlConfigLoader
  {
      Task<T> LoadAsync<T>(string yamlPath, CancellationToken ct = default);
      Task<Dictionary<string, object>> LoadDynamicAsync(string yamlPath, CancellationToken ct = default);
  }
  ```

- [ ] **YamlConfigLoader.cs** - YamlDotNet implementation
  ```csharp
  public class YamlConfigLoader : IYamlConfigLoader
  {
      private readonly IDeserializer _deserializer;

      public YamlConfigLoader()
      {
          _deserializer = new DeserializerBuilder()
              .WithNamingConvention(CamelCaseNamingConvention.Instance)
              .Build();
      }

      public async Task<T> LoadAsync<T>(string yamlPath, CancellationToken ct = default)
      {
          var content = await File.ReadAllTextAsync(yamlPath, ct);
          return _deserializer.Deserialize<T>(content);
      }
  }
  ```

### 6.2 Configuration Models

**Location**: `Scanner111.Common/Models/Configuration/`

#### Tasks

- [ ] **GameConfiguration.cs** - Game-specific configuration
  ```csharp
  public record GameConfiguration
  {
      public string GameName { get; init; } = string.Empty;
      public string XseAcronym { get; init; } = string.Empty;
      public IReadOnlyList<string> GameHints { get; init; } = Array.Empty<string>();
      public IReadOnlyList<string> RecordList { get; init; } = Array.Empty<string>();
      public Dictionary<string, string> CrashGeneratorVersions { get; init; } = new();
  }
  ```

- [ ] **ModDatabase.cs** - Mod detection configuration
  ```csharp
  public record ModEntry
  {
      public string Name { get; init; } = string.Empty;
      public IReadOnlyList<string> PluginPatterns { get; init; } = Array.Empty<string>();
      public string Category { get; init; } = string.Empty; // CONFLICT, FREQUENTLY, SOLUTIONS, etc.
      public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
  }

  public record ModDatabase
  {
      public IReadOnlyList<ModEntry> ConflictingMods { get; init; } = Array.Empty<ModEntry>();
      public IReadOnlyList<ModEntry> FrequentlyCrashing { get; init; } = Array.Empty<ModEntry>();
      public IReadOnlyList<ModEntry> WithSolutions { get; init; } = Array.Empty<ModEntry>();
      public IReadOnlyList<ModEntry> CoreMods { get; init; } = Array.Empty<ModEntry>();
  }
  ```

### 6.3 Configuration Cache

**Location**: `Scanner111.Common/Services/Configuration/`

#### Tasks

- [ ] **IConfigurationCache.cs** - Cache interface
  ```csharp
  public interface IConfigurationCache
  {
      Task<GameConfiguration> GetGameConfigAsync(string gameName, CancellationToken ct = default);
      Task<ModDatabase> GetModDatabaseAsync(string gameName, CancellationToken ct = default);
      Task<SuspectPatterns> GetSuspectPatternsAsync(string gameName, CancellationToken ct = default);
      void Clear();
  }
  ```

- [ ] **ConfigurationCache.cs** - Thread-safe caching
  ```csharp
  public class ConfigurationCache : IConfigurationCache
  {
      private readonly IYamlConfigLoader _loader;
      private readonly ConcurrentDictionary<string, GameConfiguration> _gameConfigs = new();
      private readonly ConcurrentDictionary<string, ModDatabase> _modDatabases = new();

      public async Task<GameConfiguration> GetGameConfigAsync(
          string gameName,
          CancellationToken ct = default)
      {
          return await _gameConfigs.GetOrAdd(gameName, async key =>
          {
              var path = Path.Combine("CLASSIC Data", "databases", $"CLASSIC {key}.yaml");
              return await _loader.LoadAsync<GameConfiguration>(path, ct);
          });
      }
  }
  ```

### 6.4 Testing

**Location**: `Scanner111.Common.Tests/Services/Configuration/`

#### Tasks

- [ ] **YamlConfigLoaderTests.cs**
  ```csharp
  [Fact]
  public async Task LoadAsync_WithValidYaml_DeserializesCorrectly()
  {
      var loader = new YamlConfigLoader();
      var testYaml = "game_name: Fallout 4\nxse_acronym: F4SE";

      // Write temp file and test loading
  }
  ```

- [ ] **ConfigurationCacheTests.cs** - Test caching behavior

**Acceptance Criteria**:
- ✅ Successfully loads YAML from `Code_To_Port/CLASSIC Data/databases/`
- ✅ Cache prevents redundant file reads
- ✅ Thread-safe concurrent access
- ✅ Handles missing/invalid YAML gracefully
- ✅ 90%+ test coverage

---

## Phase 7: Database & FormID Analysis

**Goal**: Implement FormID database lookups
**Dependencies**: Phases 1, 2
**Estimated Effort**: 2 weeks

### 7.1 Database Connection

**Location**: `Scanner111.Common/Services/Database/`

#### Tasks

- [ ] **Choose Database Library**
  - **Microsoft.Data.Sqlite** (Recommended for SQLite)
  - **Dapper** (Lightweight ORM for easier queries)

- [ ] **IDatabaseConnectionFactory.cs** - Connection factory
  ```csharp
  public interface IDatabaseConnectionFactory
  {
      Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default);
  }
  ```

- [ ] **SqliteDatabaseConnectionFactory.cs** - SQLite implementation
  ```csharp
  public class SqliteDatabaseConnectionFactory : IDatabaseConnectionFactory
  {
      private readonly string _connectionString;

      public SqliteDatabaseConnectionFactory(string dbPath)
      {
          _connectionString = $"Data Source={dbPath};Mode=ReadOnly";
      }

      public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
      {
          var connection = new SqliteConnection(_connectionString);
          await connection.OpenAsync(ct);
          return connection;
      }
  }
  ```

### 7.2 FormID Analyzer

**Location**: `Scanner111.Common/Services/Analysis/`

#### Tasks

- [ ] **IFormIdAnalyzer.cs** - FormID analysis interface
  ```csharp
  public interface IFormIdAnalyzer
  {
      Task<FormIdAnalysisResult> AnalyzeAsync(
          IReadOnlyList<LogSegment> segments,
          CancellationToken ct = default);

      Task<IReadOnlyDictionary<string, string>> LookupFormIdsAsync(
          IReadOnlyList<string> formIds,
          CancellationToken ct = default);
  }
  ```

- [ ] **FormIdAnalyzer.cs** - Database-backed analyzer
  ```csharp
  public class FormIdAnalyzer : IFormIdAnalyzer
  {
      private readonly IDatabaseConnectionFactory _connectionFactory;
      private static readonly Regex FormIdRegex = new(
          @"\b([0-9A-Fa-f]{8})\b",
          RegexOptions.Compiled
      );

      public async Task<IReadOnlyDictionary<string, string>> LookupFormIdsAsync(
          IReadOnlyList<string> formIds,
          CancellationToken ct = default)
      {
          await using var connection = await _connectionFactory.CreateConnectionAsync(ct);

          // Batch query for performance
          var sql = @"
              SELECT FormID, RecordName
              FROM FormIDDatabase
              WHERE FormID IN @FormIds";

          var results = await connection.QueryAsync<FormIdRecord>(
              sql,
              new { FormIds = formIds },
              cancellationToken: ct);

          return results.ToDictionary(r => r.FormID, r => r.RecordName);
      }
  }
  ```
  **Reference**: `Code_To_Port/ClassicLib/ScanLog/FormIDAnalyzerCore.py`

### 7.3 Database Pool (Optional Optimization)

**Location**: `Scanner111.Common/Services/Database/`

#### Tasks

- [ ] **DatabasePool.cs** - Connection pooling
  ```csharp
  public class DatabasePool : IAsyncDisposable
  {
      private readonly SemaphoreSlim _poolSemaphore;
      private readonly Stack<IDbConnection> _availableConnections = new();

      // Manage connection pool for concurrent FormID lookups
      // Reference: DatabasePoolManager in Python code
  }
  ```

### 7.4 Testing

**Location**: `Scanner111.Common.Tests/Services/Database/`

#### Tasks

- [ ] **FormIdAnalyzerTests.cs**
  ```csharp
  [Fact]
  public async Task LookupFormIdsAsync_WithValidFormIds_ReturnsRecordNames()
  {
      var analyzer = new FormIdAnalyzer(mockConnectionFactory);
      var formIds = new[] { "00012E46", "00014132" };

      var results = await analyzer.LookupFormIdsAsync(formIds);

      results.Should().NotBeEmpty();
  }
  ```

**Acceptance Criteria**:
- ✅ Connects to SQLite FormID database
- ✅ Batch queries for performance
- ✅ Async database operations throughout
- ✅ Connection pooling for concurrent access
- ✅ 85%+ test coverage

---

## Phase 8: GUI Integration (Avalonia)

**Goal**: Create Avalonia MVVM GUI
**Dependencies**: Phases 1-7 (all business logic)
**Estimated Effort**: 3-4 weeks

### 8.1 Main Window

**Location**: `Scanner111/Views/` and `Scanner111/ViewModels/`

#### Tasks

- [ ] **MainWindow.axaml** - Main UI layout
  ```xml
  <Window xmlns="https://github.com/avaloniaui"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:vm="using:Scanner111.ViewModels"
          x:DataType="vm:MainWindowViewModel"
          Title="Scanner111 - Crash Log Scanner">

      <DockPanel>
          <!-- Menu Bar -->
          <Menu DockPanel.Dock="Top">
              <MenuItem Header="_File">
                  <MenuItem Header="_Settings" Command="{Binding OpenSettingsCommand}" />
                  <MenuItem Header="_Exit" Command="{Binding ExitCommand}" />
              </MenuItem>
          </Menu>

          <!-- Main Content -->
          <Grid RowDefinitions="Auto,*,Auto">
              <!-- Scan Configuration -->
              <StackPanel Grid.Row="0" Margin="10">
                  <TextBlock Text="Scan Configuration" FontSize="16" FontWeight="Bold" />
                  <CheckBox Content="FCX Mode" IsChecked="{Binding FcxMode}" />
                  <CheckBox Content="Show FormID Values" IsChecked="{Binding ShowFormIds}" />
              </StackPanel>

              <!-- Results Grid -->
              <DataGrid Grid.Row="1" Items="{Binding ScanResults}" />

              <!-- Status Bar -->
              <StackPanel Grid.Row="2" Orientation="Horizontal">
                  <TextBlock Text="{Binding StatusText}" />
                  <ProgressBar Value="{Binding Progress}" Maximum="100" />
              </StackPanel>
          </Grid>
      </DockPanel>
  </Window>
  ```

- [ ] **MainWindowViewModel.cs** - ViewModel
  ```csharp
  public class MainWindowViewModel : ViewModelBase
  {
      private readonly IScanExecutor _scanExecutor;

      public ReactiveCommand<Unit, Unit> ScanCommand { get; }
      public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

      [Reactive] public bool FcxMode { get; set; }
      [Reactive] public bool ShowFormIds { get; set; }
      [Reactive] public string StatusText { get; set; } = "Ready";
      [Reactive] public double Progress { get; set; }

      public MainWindowViewModel(IScanExecutor scanExecutor)
      {
          _scanExecutor = scanExecutor;

          ScanCommand = ReactiveCommand.CreateFromTask(ExecuteScanAsync);
          OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
      }

      private async Task ExecuteScanAsync()
      {
          StatusText = "Scanning...";

          var config = new ScanConfig
          {
              FcxMode = FcxMode,
              ShowFormIdValues = ShowFormIds
          };

          var progress = new Progress<ScanProgress>(p =>
          {
              Progress = (double)p.FilesProcessed / p.TotalFiles * 100;
              StatusText = $"Processing: {p.CurrentFile}";
          });

          var result = await _scanExecutor.ExecuteScanAsync(config, progress);

          StatusText = $"Complete - Scanned: {result.Statistics.Scanned}, Failed: {result.Statistics.Failed}";
      }
  }
  ```

### 8.2 Settings Window

**Location**: `Scanner111/Views/` and `Scanner111/ViewModels/`

#### Tasks

- [ ] **SettingsWindow.axaml** - Settings UI
- [ ] **SettingsViewModel.cs** - Settings ViewModel
  ```csharp
  public class SettingsViewModel : ViewModelBase
  {
      [Reactive] public string ScanPath { get; set; } = string.Empty;
      [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
      [Reactive] public int MaxConcurrent { get; set; } = 50;

      public ReactiveCommand<Unit, Unit> BrowseScanPathCommand { get; }
      public ReactiveCommand<Unit, Unit> SaveCommand { get; }
  }
  ```

### 8.3 Dependency Injection

**Location**: `Scanner111/`

#### Tasks

- [ ] **App.axaml.cs** - DI container setup
  ```csharp
  public class App : Application
  {
      public static IServiceProvider Services { get; private set; } = null!;

      public override void OnFrameworkInitializationCompleted()
      {
          var services = new ServiceCollection();
          ConfigureServices(services);
          Services = services.BuildServiceProvider();

          if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
          {
              desktop.MainWindow = Services.GetRequiredService<MainWindow>();
          }

          base.OnFrameworkInitializationCompleted();
      }

      private void ConfigureServices(IServiceCollection services)
      {
          // Register services
          services.AddSingleton<IFileIOService, FileIOService>();
          services.AddSingleton<ILogParser, LogParser>();
          services.AddSingleton<IPluginAnalyzer, PluginAnalyzer>();
          services.AddTransient<ILogOrchestrator, LogOrchestrator>();
          services.AddSingleton<IScanExecutor, ScanExecutor>();

          // Register ViewModels
          services.AddTransient<MainWindowViewModel>();
          services.AddTransient<SettingsViewModel>();

          // Register Views
          services.AddTransient<MainWindow>();
      }
  }
  ```

### 8.4 Testing

**Location**: `Scanner111.Tests/`

#### Tasks

- [ ] **MainWindowViewModelTests.cs**
  ```csharp
  [Fact]
  public async Task ScanCommand_WhenExecuted_UpdatesProgress()
  {
      var mockExecutor = new Mock<IScanExecutor>();
      var vm = new MainWindowViewModel(mockExecutor.Object);

      await vm.ScanCommand.Execute();

      vm.StatusText.Should().Contain("Complete");
  }
  ```

- [ ] **Integration tests with Avalonia.Headless**
  ```csharp
  [Fact]
  public async Task MainWindow_WhenScanButtonClicked_ExecutesScan()
  {
      // Use Avalonia.Headless for UI testing
  }
  ```

**Acceptance Criteria**:
- ✅ Fully functional MVVM GUI
- ✅ ReactiveUI commands and properties
- ✅ Progress reporting during scan
- ✅ Settings persistence
- ✅ Dependency injection for testability
- ✅ 70%+ test coverage (ViewModels)

---

## Phase 9: Testing & Validation

**Goal**: Comprehensive testing with sample logs
**Dependencies**: Phases 1-8
**Estimated Effort**: 2-3 weeks

### 9.1 Integration Tests

**Location**: `Scanner111.Common.Tests/Integration/`

#### Tasks

- [ ] **EndToEndScanTests.cs** - Full pipeline tests
  ```csharp
  [Theory]
  [MemberData(nameof(GetSampleLogs), "sample_logs/FO4", 10)] // Test 10 random FO4 logs
  [MemberData(nameof(GetSampleLogs), "sample_logs/Skyrim", 10)]
  public async Task ScanPipeline_WithSampleLogs_ProducesValidReports(string logPath)
  {
      var executor = CreateScanExecutor();
      var config = new ScanConfig { CustomPaths = new Dictionary<string, string>() };

      // Create temp directory for output
      var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
      Directory.CreateDirectory(tempDir);

      try
      {
          // Copy log to temp directory
          var tempLogPath = Path.Combine(tempDir, Path.GetFileName(logPath));
          File.Copy(logPath, tempLogPath);

          // Execute scan
          var result = await executor.ExecuteScanAsync(config);

          // Verify report was created
          var reportPath = tempLogPath.Replace(".log", "-AUTOSCAN.md");
          File.Exists(reportPath).Should().BeTrue();

          // Verify report content
          var report = await File.ReadAllTextAsync(reportPath);
          report.Should().Contain("# Crash Log Analysis");
          report.Should().NotBeEmpty();
      }
      finally
      {
          Directory.Delete(tempDir, recursive: true);
      }
  }

  public static IEnumerable<object[]> GetSampleLogs(string directory, int count)
  {
      var logs = Directory.GetFiles(directory, "*.log");
      return logs.OrderBy(_ => Random.Shared.Next())
          .Take(count)
          .Select(log => new object[] { log });
  }
  ```

### 9.2 Validation Against Original

**Location**: `Scanner111.Common.Tests/Validation/`

#### Tasks

- [ ] **Create validation test suite**
  ```csharp
  [Fact]
  public async Task Scanner111_ProducesEquivalentReports_ToOriginalCLASSIC()
  {
      // 1. Run original CLASSIC CLI on sample log
      // 2. Run Scanner111 on same log
      // 3. Compare outputs (structure, key findings)
      // Note: Exact match not required, but should have equivalent information
  }
  ```

- [ ] **Benchmark performance**
  ```csharp
  [Fact]
  public async Task Scanner111_ProcessesLogs_InReasonableTime()
  {
      var logs = GetSampleLogs("sample_logs/FO4", 100);
      var sw = Stopwatch.StartNew();

      // Process 100 logs
      await executor.ExecuteScanAsync(config);

      sw.Stop();
      sw.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2)); // Target: <2 min for 100 logs
  }
  ```

### 9.3 Edge Case Testing

**Location**: `Scanner111.Common.Tests/EdgeCases/`

#### Tasks

- [ ] **Incomplete logs** - Logs with missing sections
- [ ] **Malformed logs** - Invalid UTF-8, truncated data
- [ ] **Empty logs** - Zero-byte files
- [ ] **Huge logs** - Very large crash logs (>10MB)
- [ ] **Special characters** - Non-ASCII plugin names, paths

**Acceptance Criteria**:
- ✅ Successfully processes all 1,312 sample logs without crashes
- ✅ Report output is valid markdown
- ✅ Performance is acceptable (target: <100ms per log)
- ✅ Edge cases handled gracefully (no exceptions, meaningful error messages)
- ✅ Validation tests confirm feature parity with original CLASSIC

---

## Phase 10: Optimization & Polish

**Goal**: Performance optimization and final polish
**Dependencies**: Phase 9
**Estimated Effort**: 1-2 weeks

### 10.1 Performance Optimization

#### Tasks

- [ ] **Profile with BenchmarkDotNet**
  ```csharp
  [MemoryDiagnoser]
  public class LogParsingBenchmarks
  {
      private string _sampleLog = null!;

      [GlobalSetup]
      public void Setup()
      {
          _sampleLog = File.ReadAllText("sample_logs/FO4/crash-12624.log");
      }

      [Benchmark]
      public void ParseLog() => _parser.ParseAsync(_sampleLog).GetAwaiter().GetResult();
  }
  ```

- [ ] **Optimize regex compilation** - Ensure all regex patterns are compiled
- [ ] **Cache YAML configs** - Verify caching is working efficiently
- [ ] **Reduce allocations** - Use `Span<T>`, `ReadOnlySpan<T>` where beneficial
- [ ] **Parallel analysis** - Maximize use of Task.WhenAll

### 10.2 Code Quality

#### Tasks

- [ ] **Run static analysis**
  - Enable nullable reference type warnings
  - Fix all compiler warnings
  - Run Roslyn analyzers

- [ ] **Code coverage** - Aim for 80%+ overall coverage
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage
  ```

- [ ] **Documentation** - Ensure all public APIs have XML comments

### 10.3 Final Testing

#### Tasks

- [ ] **User acceptance testing** - Manual testing of GUI workflows
- [ ] **Cross-platform testing** - Verify on Windows (primary), Linux, macOS
- [ ] **Stress testing** - Test with 1,000+ logs
- [ ] **Memory leak testing** - Verify no memory leaks during long runs

**Acceptance Criteria**:
- ✅ No compiler warnings
- ✅ 80%+ code coverage
- ✅ All 1,312 sample logs process successfully
- ✅ Performance meets targets (<100ms per log)
- ✅ GUI is responsive and intuitive
- ✅ Complete XML documentation

---

## Dependencies & Libraries

### Required NuGet Packages

#### Scanner111.Common (Business Logic)
```xml
<ItemGroup>
  <!-- YAML -->
  <PackageReference Include="YamlDotNet" Version="15.1.0" />

  <!-- Database -->
  <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
  <PackageReference Include="Dapper" Version="2.1.35" />

  <!-- Utilities -->
  <PackageReference Include="System.Linq.Async" Version="6.0.1" />
</ItemGroup>
```

#### Scanner111.Common.Tests
```xml
<ItemGroup>
  <!-- Testing -->
  <PackageReference Include="xunit" Version="2.9.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
  <PackageReference Include="Moq" Version="4.20.70" />

  <!-- Coverage -->
  <PackageReference Include="coverlet.collector" Version="6.0.2" />
</ItemGroup>
```

#### Scanner111 (GUI)
```xml
<ItemGroup>
  <!-- Avalonia (already present) -->
  <PackageReference Include="Avalonia" Version="11.3.8" />
  <PackageReference Include="Avalonia.ReactiveUI" Version="11.3.8" />

  <!-- Dependency Injection -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
</ItemGroup>
```

#### Scanner111.Tests
```xml
<ItemGroup>
  <!-- Headless UI Testing -->
  <PackageReference Include="Avalonia.Headless" Version="11.3.8" />
  <PackageReference Include="Avalonia.Headless.XUnit" Version="11.3.8" />
</ItemGroup>
```

---

## Python to C# Translation Guide

### Common Patterns

| Python Pattern | C# Equivalent | Notes |
|----------------|---------------|-------|
| `@dataclass` | `record` | Use records for immutable data |
| `tuple[str, ...]` | `IReadOnlyList<string>` | Prefer IReadOnly* interfaces |
| `async def func()` | `async Task FuncAsync()` | All async methods return Task/Task<T> |
| `asyncio.run()` | `await` at top level | Use async all the way |
| `Path` (pathlib) | `Path.Combine()` | C# has System.IO.Path utilities |
| `with open()` | `using var stream` | IDisposable pattern |
| `dict[str, int]` | `Dictionary<string, int>` | Generic collections |
| `if __name__ == "__main__"` | `Program.Main()` | Entry point pattern |
| `@staticmethod` | `static` method | Static methods |
| `Optional[T]` | `T?` (nullable) | Nullable reference types |
| `re.compile()` | `new Regex(..., Compiled)` | Compiled regex for performance |
| `enumerate()` | `.Select((item, idx) => ...)` | LINQ Select with index |
| `yield` | `yield return` | Iterator pattern |
| List comprehension | LINQ | `[x*2 for x in items]` → `items.Select(x => x * 2)` |

### Async Patterns

**Python (asyncio)**:
```python
async def process_logs_async(logs: list[Path]) -> list[Result]:
    tasks = [process_log(log) for log in logs]
    return await asyncio.gather(*tasks)
```

**C# (Task)**:
```csharp
async Task<IReadOnlyList<Result>> ProcessLogsAsync(IReadOnlyList<string> logs)
{
    var tasks = logs.Select(log => ProcessLogAsync(log));
    return await Task.WhenAll(tasks);
}
```

### Immutable Collections

**Python**:
```python
@dataclass(frozen=True)
class ReportFragment:
    content: tuple[str, ...]
```

**C#**:
```csharp
public record ReportFragment
{
    public IReadOnlyList<string> Content { get; init; } = Array.Empty<string>();
}
```

### Configuration Loading

**Python (YAML + AsyncBridge)**:
```python
settings = YamlSettingsCache.get_batch(["game_info", "mods_conf"])
```

**C# (YamlDotNet + async)**:
```csharp
var settings = await _configCache.GetBatchAsync(
    new[] { "game_info", "mods_conf" });
```

---

## Testing Strategy

### Test Pyramid

```
        /\
       /  \
      / E2E \         10 tests  - Full pipeline with sample logs
     /______\
    /        \
   /Integration\      50 tests  - Multi-component integration
  /____________\
 /              \
/   Unit Tests   \   200 tests - Individual components
/________________\
```

### Coverage Goals

- **Business Logic (Scanner111.Common)**: 85%+
- **ViewModels**: 80%+
- **Views**: 50% (integration tests only)
- **Overall**: 80%+

### Test Categories

1. **Unit Tests** - Fast, isolated, no I/O
   - Models
   - Parsers (with in-memory strings)
   - Analyzers (with mocked dependencies)

2. **Integration Tests** - Multi-component, file I/O
   - File I/O + Parser
   - Orchestrator + Analyzers
   - Configuration loading

3. **End-to-End Tests** - Full pipeline
   - Sample log → Report
   - Batch scanning
   - GUI workflows (Headless)

### Sample Log Testing

**Strategy**: Create test fixtures from sample logs
```csharp
public class SampleLogFixtures : IClassFixture<SampleLogFixtures>
{
    public IReadOnlyList<string> FO4Logs { get; }
    public IReadOnlyList<string> SkyrimLogs { get; }

    public SampleLogFixtures()
    {
        FO4Logs = Directory.GetFiles("sample_logs/FO4", "*.log").ToList();
        SkyrimLogs = Directory.GetFiles("sample_logs/Skyrim", "*.log").ToList();
    }
}
```

---

## Milestones & Acceptance Criteria

### Milestone 1: Foundation Complete
**Target**: Weeks 1-2
**Criteria**:
- ✅ All Phase 1 models implemented and tested
- ✅ 100% test coverage on models
- ✅ CI/CD pipeline setup
- ✅ Code quality checks passing

### Milestone 2: Parsing Complete
**Target**: Weeks 3-5
**Criteria**:
- ✅ All 1,312 sample logs parse without exceptions
- ✅ Segment extraction matches original CLASSIC
- ✅ 90%+ test coverage

### Milestone 3: Analysis Complete
**Target**: Weeks 6-9
**Criteria**:
- ✅ Plugin analysis functional
- ✅ Suspect detection functional
- ✅ Settings scanning functional
- ✅ 85%+ test coverage

### Milestone 4: Orchestration Complete
**Target**: Weeks 10-12
**Criteria**:
- ✅ End-to-end pipeline working
- ✅ Concurrent execution functional
- ✅ Report generation validated
- ✅ Performance acceptable (<100ms per log)

### Milestone 5: GUI Complete
**Target**: Weeks 13-16
**Criteria**:
- ✅ Functional Avalonia GUI
- ✅ Settings management
- ✅ Progress reporting
- ✅ User-friendly workflows

### Milestone 6: Feature Parity
**Target**: Weeks 17-20
**Criteria**:
- ✅ All ~250 checks implemented
- ✅ All 6 mod database categories integrated
- ✅ FormID database lookups working
- ✅ FCX mode functional
- ✅ Validation tests confirm equivalence to original

---

## Next Steps

### Immediate Actions (This Session)

1. **Set up project structure** (if not already done)
2. **Install dependencies** (NuGet packages)
3. **Start Phase 1** - Implement core models
4. **Create first tests** - Establish testing patterns

### Session Planning

- **Session 1-2**: Phase 1 (Foundation)
- **Session 3-5**: Phase 2 (Parsing)
- **Session 6-8**: Phase 3 (Analysis)
- **Session 9-10**: Phase 4 (Reporting)
- **Session 11-12**: Phase 5 (Orchestration)
- **Session 13-14**: Phase 6 (Configuration)
- **Session 15**: Phase 7 (Database)
- **Session 16-18**: Phase 8 (GUI)
- **Session 19-20**: Phase 9-10 (Testing & Polish)

### Reference Materials

Throughout development, refer to:
- **Code_To_Port/** - Original implementation
- **sample_logs/** - Testing data (1,312 logs)
- **CLAUDE.md** - Development guidelines
- **This roadmap** - Phase-by-phase plan

---

**Document Status**: Living document - update as phases complete
