# Scanner 111 C# Implementation Guide for AI Assistants

## Project Overview
Port CLASSIC Python crash log analyzer to C# using Avalonia MVVM framework. The application analyzes Bethesda game crash logs (Buffout 4/Crash Logger format) with both GUI and CLI interfaces.

## Solution Structure

```
Scanner111/
├── Scanner111.sln
├── Scanner111.Core/                 # .NET 8.0 Class Library
├── Scanner111.GUI/                  # .NET 8.0 Avalonia Application  
├── Scanner111.CLI/                  # .NET 8.0 Console Application
└── Scanner111.Tests/                # .NET 8.0 xUnit Test Project
```

## Phase 1: Core Library Foundation ✅

### Checklist: Project Setup
- [x] Create solution: `dotnet new sln -n Scanner111`
- [x] Create projects:
  ```bash
  dotnet new classlib -n Scanner111.Core -f net8.0
  dotnet new avalonia.mvvm -n Scanner111.GUI -f net8.0
  dotnet new console -n Scanner111.CLI -f net8.0
  dotnet new xunit -n Scanner111.Tests -f net8.0
  ```
- [x] Add project references:
  ```bash
  dotnet add Scanner111.GUI reference Scanner111.Core
  dotnet add Scanner111.CLI reference Scanner111.Core
  dotnet add Scanner111.Tests reference Scanner111.Core
  ```
- [x] Install NuGet packages:
  ```xml
  <!-- Scanner111.Core.csproj -->
  <PackageReference Include="YamlDotNet" Version="13.7.1" />
  <PackageReference Include="System.Data.SQLite" Version="1.0.118" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
  
  <!-- Scanner111.GUI.csproj -->
  <PackageReference Include="Avalonia" Version="11.0.6" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.6" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.6" />
  <PackageReference Include="Avalonia.ReactiveUI" Version="11.0.6" />
  <PackageReference Include="MessageBox.Avalonia" Version="3.1.5" />
  
  <!-- Scanner111.CLI.csproj -->
  <PackageReference Include="CommandLineParser" Version="2.9.1" />
  <PackageReference Include="Spectre.Console" Version="0.48.0" />
  ```

### Checklist: Core Models Implementation

#### Task: Create CrashLog.cs
**File**: `Scanner111.Core/Models/CrashLog.cs`
```csharp
namespace Scanner111.Core.Models;

public class CrashLog
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<string> OriginalLines { get; init; } = new();
    public string Content => string.Join("\n", OriginalLines);
    
    // Parsed sections
    public string MainError { get; set; } = string.Empty;
    public List<string> CallStack { get; set; } = new();
    public Dictionary<string, string> Plugins { get; set; } = new(); // filename -> loadOrder
    public string CrashGenVersion { get; set; } = string.Empty;
    public DateTime? CrashTime { get; set; }
    
    // Validation
    public bool IsComplete => Plugins.Count > 0;
    public bool HasError => !string.IsNullOrEmpty(MainError);
}
```
- [ ] Implement CrashLog class
- [ ] Add XML documentation
- [ ] Create unit tests in `Scanner111.Tests/Models/CrashLogTests.cs`

#### Task: Create ScanResult.cs
**File**: `Scanner111.Core/Models/ScanResult.cs`
```csharp
namespace Scanner111.Core.Models;

public class ScanResult
{
    public required string LogPath { get; init; }
    public List<string> Report { get; init; } = new();
    public bool Failed { get; init; }
    public ScanStatistics Statistics { get; init; } = new();
    
    public string ReportText => string.Join("", Report);
    public string OutputPath => Path.ChangeExtension(LogPath, null) + "-AUTOSCAN.md";
}

public class ScanStatistics : Dictionary<string, int>
{
    public ScanStatistics()
    {
        this["scanned"] = 0;
        this["incomplete"] = 0;
        this["failed"] = 0;
    }
}
```
- [ ] Implement ScanResult class
- [ ] Implement ScanStatistics with proper initialization
- [ ] Add convenience methods for statistics

#### Task: Create Configuration Models
**File**: `Scanner111.Core/Models/Configuration.cs`
```csharp
namespace Scanner111.Core.Models;

public class ClassicScanLogsInfo
{
    public string ClassicVersion { get; set; } = "7.35.0";
    public string CrashgenName { get; set; } = "Buffout 4";
    public List<string> ClassicGameHints { get; set; } = new();
    public string AutoscanText { get; set; } = string.Empty;
    
    // Suspect patterns from YAML
    public Dictionary<string, string> SuspectsErrorList { get; set; } = new();
    public Dictionary<string, string> SuspectsStackList { get; set; } = new();
    public List<string> IgnorePluginsList { get; set; } = new();
    
    // Named records patterns
    public Dictionary<string, List<string>> NamedRecordsType { get; set; } = new();
}
```
- [ ] Create all configuration model classes
- [ ] Match Python dataclass structure exactly

### Checklist: Infrastructure Implementation

#### Task: Implement YamlSettingsCache
**File**: `Scanner111.Core/Infrastructure/YamlSettingsCache.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public static class YamlSettingsCache
{
    private static readonly Dictionary<string, object?> _cache = new();
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    
    public static T? YamlSettings<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        var cacheKey = $"{yamlFile}:{keyPath}";
        
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (T?)cached;
        
        try
        {
            var yamlPath = Path.Combine("CLASSIC Data", "databases", $"{yamlFile}.yaml");
            if (!File.Exists(yamlPath))
                return defaultValue;
            
            var yaml = File.ReadAllText(yamlPath);
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
            
            // Navigate key path (e.g., "CLASSIC_Settings.Show FormID Values")
            var value = NavigateKeyPath(data, keyPath);
            
            _cache[cacheKey] = value;
            return value != null ? (T)value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public static void SetYamlSetting<T>(string yamlFile, string keyPath, T value)
    {
        // Implementation for saving settings
    }
}
```
- [ ] Implement YAML loading with YamlDotNet
- [ ] Implement key path navigation (dot notation)
- [ ] Add caching mechanism
- [ ] Create unit tests for various data types
- [ ] Handle missing files gracefully

#### Task: Implement MessageHandler
**File**: `Scanner111.Core/Infrastructure/MessageHandler.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public interface IMessageHandler
{
    void ShowInfo(string message, MessageTarget target = MessageTarget.All);
    void ShowWarning(string message);
    void ShowError(string message);
    IProgress<ProgressInfo> ShowProgress(string title, int totalItems);
}

public enum MessageTarget
{
    All,
    GuiOnly,
    CliOnly
}

public class ProgressInfo
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = string.Empty;
}

public static class MessageHandler
{
    private static IMessageHandler? _handler;
    
    public static void Initialize(IMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
    
    public static void MsgInfo(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowInfo(message, target);
    }
    
    public static void MsgWarning(string message)
    {
        _handler?.ShowWarning(message);
    }
    
    public static void MsgError(string message)
    {
        _handler?.ShowError(message);
    }
    
    public static IProgress<ProgressInfo> MsgProgress(string title, int totalItems)
    {
        return _handler?.ShowProgress(title, totalItems) ?? new NullProgress();
    }
}
```
- [ ] Create IMessageHandler interface
- [ ] Implement static MessageHandler class
- [ ] Add all message types (Info, Warning, Error, Progress)
- [ ] Include MessageTarget enum
- [ ] Create NullProgress implementation for safety

#### Task: Implement GlobalRegistry
**File**: `Scanner111.Core/Infrastructure/GlobalRegistry.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public static class GlobalRegistry
{
    private static readonly Dictionary<string, object> _registry = new();
    
    public static void Set<T>(string key, T value) where T : notnull
    {
        _registry[key] = value;
    }
    
    public static T? Get<T>(string key) where T : class
    {
        return _registry.TryGetValue(key, out var value) ? value as T : null;
    }
    
    // Convenience properties
    public static string Game => Get<string>("Game") ?? "Fallout4";
    public static string GameVR => Get<string>("GameVR") ?? "";
    public static string LocalDir => Get<string>("LocalDir") ?? AppDomain.CurrentDomain.BaseDirectory;
}
```
- [ ] Implement generic registry pattern
- [ ] Add convenience properties for common values
- [ ] Thread-safe implementation if needed

## Phase 2: Analyzer Implementation ✅

### Checklist: FormID Analyzer

#### Task: Create IAnalyzer Interface
**File**: `Scanner111.Core/Analyzers/IAnalyzer.cs`
```csharp
namespace Scanner111.Core.Analyzers;

public interface IAnalyzer
{
    string Name { get; }
    Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
}

public abstract class AnalysisResult
{
    public required string AnalyzerName { get; init; }
    public bool HasFindings { get; init; }
    public List<string> ReportLines { get; init; } = new();
}
```
- [ ] Create base analyzer interface
- [ ] Create AnalysisResult base class
- [ ] Add cancellation token support

#### Task: Implement FormIdAnalyzer
**File**: `Scanner111.Core/Analyzers/FormIdAnalyzer.cs`
```csharp
namespace Scanner111.Core.Analyzers;

public class FormIdAnalyzer : IAnalyzer
{
    private static readonly Regex FormIdPattern = new(
        @"^\s*Form ID:\s*0x([0-9A-F]{8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private readonly bool _showFormIdValues;
    private readonly bool _formIdDbExists;
    private readonly FormIdDatabase? _database;
    
    public string Name => "FormID Analyzer";
    
    public FormIdAnalyzer(ClassicScanLogsInfo config, bool showFormIdValues, bool formIdDbExists)
    {
        _showFormIdValues = showFormIdValues;
        _formIdDbExists = formIdDbExists;
        
        if (_formIdDbExists)
        {
            _database = new FormIdDatabase();
        }
    }
    
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        var formIds = ExtractFormIds(crashLog.CallStack);
        var report = new List<string>();
        
        GenerateFormIdReport(formIds, crashLog.Plugins, report);
        
        return new FormIdAnalysisResult
        {
            AnalyzerName = Name,
            FormIds = formIds,
            ReportLines = report,
            HasFindings = formIds.Count > 0
        };
    }
    
    private List<string> ExtractFormIds(List<string> callStack)
    {
        // Direct port of Python logic
        var formIds = new List<string>();
        
        foreach (var line in callStack)
        {
            var match = FormIdPattern.Match(line);
            if (match.Success)
            {
                var formId = match.Groups[1].Value.ToUpper();
                if (!formId.StartsWith("FF"))
                {
                    formIds.Add($"Form ID: {formId}");
                }
            }
        }
        
        return formIds;
    }
}
```
- [ ] Implement FormIdAnalyzer class
- [ ] Port ExtractFormIds method exactly
- [ ] Port FormIdMatch method exactly
- [ ] Add database lookup support
- [ ] Create unit tests with sample data

### Checklist: Other Analyzers

#### Task: Implement PluginAnalyzer
**File**: `Scanner111.Core/Analyzers/PluginAnalyzer.cs`
- [ ] Create PluginAnalyzer class
- [ ] Port plugin_match method
- [ ] Handle ignore list
- [ ] Format output exactly like Python

#### Task: Implement SuspectScanner
**File**: `Scanner111.Core/Analyzers/SuspectScanner.cs`
- [ ] Create SuspectScanner class
- [ ] Port suspect_scan_mainerror method
- [ ] Port suspect_scan_stack method
- [ ] Load patterns from YAML

#### Task: Implement SettingsScanner
**File**: `Scanner111.Core/Analyzers/SettingsScanner.cs`
- [ ] Create SettingsScanner class
- [ ] Port all validation methods
- [ ] Handle FCX mode checks

#### Task: Implement RecordScanner
**File**: `Scanner111.Core/Analyzers/RecordScanner.cs`
- [ ] Create RecordScanner class
- [ ] Port scan_named_records method
- [ ] Handle type-specific patterns

## Phase 3: Orchestrator Pattern ✅

### Checklist: Orchestrator Implementation

#### Task: Create IScanOrchestrator Interface
**File**: `Scanner111.Core/Pipeline/IScanOrchestrator.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public interface IScanOrchestrator : IDisposable
{
    Task<ScanResult> ProcessCrashLogAsync(string logPath, CancellationToken cancellationToken = default);
    Task<List<ScanResult>> ProcessBatchAsync(List<string> logPaths, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);
}
```

#### Task: Implement ScanOrchestrator
**File**: `Scanner111.Core/Pipeline/ScanOrchestrator.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public class ScanOrchestrator : IScanOrchestrator
{
    private readonly ClassicScanLogsInfo _config;
    private readonly List<IAnalyzer> _analyzers;
    private readonly ReportGenerator _reportGenerator;
    private readonly CrashLogParser _parser;
    
    public ScanOrchestrator(ClassicScanLogsInfo config, bool? fcxMode, bool? showFormIdValues, bool formIdDbExists)
    {
        _config = config;
        _parser = new CrashLogParser();
        _reportGenerator = new ReportGenerator(config);
        
        // Initialize analyzers in order
        _analyzers = new List<IAnalyzer>
        {
            new PluginAnalyzer(config),
            new FormIdAnalyzer(config, showFormIdValues ?? false, formIdDbExists),
            new SuspectScanner(config),
            new RecordScanner(config),
            new SettingsScanner(config)
        };
    }
    
    public async Task<ScanResult> ProcessCrashLogAsync(string logPath, CancellationToken cancellationToken = default)
    {
        // 1. Parse crash log
        var crashLog = await _parser.ParseAsync(logPath, cancellationToken);
        
        // 2. Create report
        var report = new List<string>();
        _reportGenerator.GenerateHeader(crashLog.FileName, report);
        
        // 3. Check for errors
        if (!string.IsNullOrEmpty(crashLog.MainError))
        {
            _reportGenerator.GenerateErrorSection(crashLog, report);
        }
        
        // 4. Run each analyzer
        foreach (var analyzer in _analyzers)
        {
            var result = await analyzer.AnalyzeAsync(crashLog, cancellationToken);
            _reportGenerator.AddAnalysisResult(result, report);
        }
        
        // 5. Generate footer
        _reportGenerator.GenerateFooter(report);
        
        // 6. Determine statistics
        var stats = new ScanStatistics();
        if (!crashLog.IsComplete)
        {
            stats["incomplete"] = 1;
        }
        else if (report.Any(line => line.Contains("SUSPECT FOUND")))
        {
            stats["failed"] = 1;
        }
        else
        {
            stats["scanned"] = 1;
        }
        
        return new ScanResult
        {
            LogPath = logPath,
            Report = report,
            Failed = stats["failed"] > 0,
            Statistics = stats
        };
    }
}
```
- [ ] Implement synchronous orchestrator
- [ ] Initialize all analyzers in correct order
- [ ] Handle report generation flow
- [ ] Calculate statistics correctly
- [ ] Add error handling

#### Task: Implement AsyncScanOrchestrator
**File**: `Scanner111.Core/Pipeline/AsyncScanOrchestrator.cs`
- [ ] Extend ScanOrchestrator
- [ ] Add async FormID database support
- [ ] Implement batch processing with parallelism
- [ ] Add progress reporting
- [ ] Implement semaphore for throttling

## Phase 4: GUI Implementation ✅

### Checklist: ViewModels

#### Task: Create ViewModelBase
**File**: `Scanner111.GUI/ViewModels/ViewModelBase.cs`
```csharp
using ReactiveUI;

namespace Scanner111.GUI.ViewModels;

public abstract class ViewModelBase : ReactiveObject
{
}
```

#### Task: Create MainWindowViewModel
**File**: `Scanner111.GUI/ViewModels/MainWindowViewModel.cs`
```csharp
namespace Scanner111.GUI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly IScanPipeline _pipeline;
    private bool _isScanning;
    private string _statusMessage = "Ready";
    
    // Settings properties
    private bool _showFormIdValues;
    private bool _moveUnsolvedLogs;
    private bool? _fcxMode;
    private int _maxConcurrency = Environment.ProcessorCount;
    
    public MainWindowViewModel()
    {
        // Load settings
        LoadSettings();
        
        // Create commands
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(
            ScanCrashLogsAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));
        
        ScanGameFilesCommand = ReactiveCommand.CreateFromTask(
            ScanGameFilesAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));
        
        // Auto-save settings on change
        this.WhenAnyValue(x => x.ShowFormIdValues)
            .Skip(1) // Skip initial value
            .Subscribe(value => SaveSetting("Show FormID Values", value));
        
        this.WhenAnyValue(x => x.MaxConcurrency)
            .Skip(1)
            .Subscribe(value => SaveSetting("Max Concurrency", value));
    }
    
    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set => this.RaiseAndSetIfChanged(ref _maxConcurrency, value);
    }
    
    // Commands
    public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanGameFilesCommand { get; }
    
    private async Task ScanCrashLogsAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning crash logs...";
        
        try
        {
            var scanner = new ClassicScanLogs
            {
                FcxMode = _fcxMode,
                MaxConcurrency = _maxConcurrency
            };
            await scanner.CrashLogsScanAsync();
            StatusMessage = "Scan complete!";
        }
        catch (Exception ex)
        {
            MessageHandler.MsgError($"Scan failed: {ex.Message}");
            StatusMessage = "Scan failed";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
```
- [ ] Implement all bindable properties
- [ ] Create all commands
- [ ] Implement settings loading/saving
- [ ] Add progress reporting
- [ ] Handle errors gracefully

### Checklist: Views

#### Task: Create MainWindow.axaml
**File**: `Scanner111.GUI/Views/MainWindow.axaml`
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Scanner111.GUI.ViewModels"
        x:Class="Scanner111.GUI.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Scanner 111 - Crash Log Auto Scanner"
        Width="1200" Height="800"
        MinWidth="800" MinHeight="600">
    
    <Design.DataContext>
        <vm:MainWindowViewModel/>
    </Design.DataContext>
    
    <Grid RowDefinitions="Auto,*,Auto">
        <!-- Header -->
        <Border Grid.Row="0" Background="#2b2b2b" Height="80">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <Image Grid.Column="0" Source="/Assets/logo.png" Height="60" Margin="20,0"/>
                <TextBlock Grid.Column="1" Text="Scanner 111" FontSize="32" 
                           VerticalAlignment="Center" HorizontalAlignment="Center"
                           Foreground="White" FontWeight="Bold"/>
                <TextBlock Grid.Column="2" Text="{Binding Version}" 
                           VerticalAlignment="Center" Margin="20"
                           Foreground="Gray"/>
            </Grid>
        </Border>
        
        <!-- Main Content -->
        <TabControl Grid.Row="1" Margin="10">
            <TabItem Header="Main">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Spacing="20" Margin="20">
                        <!-- Action Buttons -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <StackPanel Spacing="10">
                                <TextBlock Text="Actions" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>
                                
                                <Button Command="{Binding ScanCrashLogsCommand}"
                                        IsEnabled="{Binding !IsScanning}"
                                        HorizontalAlignment="Stretch"
                                        Height="50"
                                        Classes="primary">
                                    <StackPanel Orientation="Horizontal" Spacing="10">
                                        <PathIcon Data="{StaticResource document_search_regular}"/>
                                        <TextBlock Text="SCAN CRASH LOGS" VerticalAlignment="Center"/>
                                    </StackPanel>
                                </Button>
                                
                                <Button Command="{Binding ScanGameFilesCommand}"
                                        IsEnabled="{Binding !IsScanning}"
                                        HorizontalAlignment="Stretch"
                                        Height="50"
                                        Classes="primary">
                                    <StackPanel Orientation="Horizontal" Spacing="10">
                                        <PathIcon Data="{StaticResource folder_search_regular}"/>
                                        <TextBlock Text="SCAN GAME FILES" VerticalAlignment="Center"/>
                                    </StackPanel>
                                </Button>
                            </StackPanel>
                        </Border>
                        
                        <!-- Settings -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <StackPanel Spacing="10">
                                <TextBlock Text="Scan Settings" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>
                                
                                <Grid ColumnDefinitions="*,*,*" RowDefinitions="Auto,Auto,Auto,Auto" 
                                      ColumnSpacing="20" RowSpacing="15">
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="0"
                                              Content="Show FormID Values"
                                              IsChecked="{Binding ShowFormIdValues}"/>
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="1"
                                              Content="Move Unsolved Logs"
                                              IsChecked="{Binding MoveUnsolvedLogs}"/>
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="2"
                                              Content="Simplify Logs"
                                              IsChecked="{Binding SimplifyLogs}"/>
                                    
                                    <!-- Add more checkboxes following the grid pattern -->
                                </Grid>
                                
                                <!-- Performance Settings -->
                                <StackPanel Orientation="Horizontal" Spacing="10" Margin="0,10,0,0">
                                    <TextBlock Text="Max Concurrency:" VerticalAlignment="Center"/>
                                    <NumericUpDown Value="{Binding MaxConcurrency}"
                                                   Minimum="1"
                                                   Maximum="32"
                                                   Width="100"/>
                                    <TextBlock Text="(concurrent operations)" 
                                               Foreground="Gray" 
                                               VerticalAlignment="Center"/>
                                </StackPanel>
                            </StackPanel>
                        </Border>
                        
                        <!-- Info Panel -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <TextBlock Text="{Binding InfoMessage}"
                                       TextWrapping="Wrap"
                                       LineHeight="20"/>
                        </Border>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
            
            <TabItem Header="Settings">
                <!-- Settings content -->
            </TabItem>
            
            <TabItem Header="Backup">
                <!-- Backup content -->
            </TabItem>
            
            <TabItem Header="Help">
                <!-- Help content -->
            </TabItem>
        </TabControl>
        
        <!-- Status Bar -->
        <Border Grid.Row="2" Background="#1a1a1a" Height="30">
            <Grid ColumnDefinitions="*,Auto">
                <TextBlock Grid.Column="0" Text="{Binding StatusMessage}" 
                           VerticalAlignment="Center" Margin="10,0"/>
                <ProgressBar Grid.Column="1" 
                             IsVisible="{Binding IsScanning}"
                             IsIndeterminate="True"
                             Width="100" Height="15" Margin="10,0"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```
- [ ] Create responsive grid layout
- [ ] Add all UI elements from Python version
- [ ] Implement proper data binding
- [ ] Add icons and styling
- [ ] Ensure all widgets remain visible when resizing

#### Task: Create App.axaml Styles
**File**: `Scanner111.GUI/App.axaml`
```xml
<Application.Styles>
    <FluentTheme />
    
    <Style Selector="Window">
        <Setter Property="Background" Value="#2d2d30"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
    </Style>
    
    <Style Selector="Button.primary">
        <Setter Property="Background" Value="#0e639c"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="CornerRadius" Value="4"/>
    </Style>
    
    <Style Selector="Button.primary:pointerover">
        <Setter Property="Background" Value="#1177bb"/>
    </Style>
    
    <Style Selector="CheckBox">
        <Setter Property="Padding" Value="8,4"/>
        <Setter Property="MinHeight" Value="32"/>
    </Style>
    
    <Style Selector="TabItem">
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Padding" Value="12,8"/>
    </Style>
</Application.Styles>
```
- [ ] Create dark theme styles
- [ ] Style all control types
- [ ] Add hover effects
- [ ] Ensure readability

### Checklist: Services

#### Task: Implement GuiMessageService
**File**: `Scanner111.GUI/Services/GuiMessageService.cs`
```csharp
namespace Scanner111.GUI.Services;

public class GuiMessageService : IMessageHandler
{
    private readonly Window _mainWindow;
    
    public GuiMessageService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }
    
    public async void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        
        await MessageBox.Avalonia.MessageBoxManager
            .GetMessageBoxStandardWindow("Information", message)
            .ShowDialog(_mainWindow);
    }
    
    public async void ShowError(string message)
    {
        await MessageBox.Avalonia.MessageBoxManager
            .GetMessageBoxStandardWindow("Error", message, ButtonEnum.Ok, Icon.Error)
            .ShowDialog(_mainWindow);
    }
    
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        var dialog = new ProgressDialog
        {
            Title = title,
            Maximum = totalItems
        };
        
        dialog.ShowDialog(_mainWindow);
        
        return new Progress<ProgressInfo>(info =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialog.Value = info.Current;
                dialog.Message = info.Message;
                
                if (info.Current >= info.Total)
                {
                    dialog.Close();
                }
            });
        });
    }
}
```
- [ ] Implement all message types
- [ ] Create progress dialog
- [ ] Handle UI thread marshaling
- [ ] Add dialog styling

## Phase 5: CLI Implementation ✅

### Checklist: CLI Setup

#### Task: Create CLI Program
**File**: `Scanner111.CLI/Program.cs`
```csharp
using CommandLine;
using Spectre.Console;

namespace Scanner111.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Initialize CLI message handler
        MessageHandler.Initialize(new CliMessageService());
        
        return await Parser.Default.ParseArguments<ScanOptions, ConfigOptions>(args)
            .MapResult(
                (ScanOptions opts) => RunScan(opts),
                (ConfigOptions opts) => RunConfig(opts),
                errs => Task.FromResult(1)
            );
    }
    
    static async Task<int> RunScan(ScanOptions options)
    {
        AnsiConsole.Write(new FigletText("Scanner 111").Color(Color.Blue));
        AnsiConsole.WriteLine();
        
        var scanner = new ClassicScanLogs
        {
            FcxMode = options.NoFcxMode ? false : options.FcxMode,
            CustomFolder = options.Folder
        };
        
        await scanner.CrashLogsScanAsync();
        return 0;
    }
    
    static async Task<int> RunConfig(ConfigOptions options)
    {
        // Handle config command
        AnsiConsole.WriteLine("Config command not implemented yet");
        return 0;
    }
}

[Verb("scan", HelpText = "Scan crash logs")]
public class ScanOptions
{
    [Option("fcx-mode", Required = false, HelpText = "Enable FCX mode")]
    public bool? FcxMode { get; set; }
    
    [Option("no-fcx-mode", Required = false, HelpText = "Disable FCX mode")]
    public bool NoFcxMode { get; set; }
    
    [Option("folder", Required = false, HelpText = "Custom crash log folder")]
    public string? Folder { get; set; }
}

[Verb("config", HelpText = "Configure application settings")]
public class ConfigOptions
{
    [Option("set", Required = false, HelpText = "Set a configuration value")]
    public string? SetValue { get; set; }
    
    [Option("get", Required = false, HelpText = "Get a configuration value")]
    public string? GetValue { get; set; }
}
```
- [ ] Set up CommandLineParser
- [ ] Add all command-line options
- [ ] Use Spectre.Console for styling
- [ ] Match Python's CLI interface

#### Task: Implement CliMessageService
**File**: `Scanner111.CLI/Services/CliMessageService.cs`
```csharp
namespace Scanner111.CLI.Services;

public class CliMessageService : IMessageHandler
{
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        AnsiConsole.MarkupLine($"[green]INFO:[/] {Markup.Escape(message)}");
    }
    
    public void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {Markup.Escape(message)}");
    }
    
    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]ERROR:[/] {Markup.Escape(message)}");
    }
    
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        var progress = AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            });
        
        ProgressTask? task = null;
        
        progress.StartAsync(async ctx =>
        {
            task = ctx.AddTask(title, maxValue: totalItems);
        });
        
        return new Progress<ProgressInfo>(info =>
        {
            task?.Increment(info.Current - (int)task.Value);
            if (!string.IsNullOrEmpty(info.Message))
            {
                task?.Description = info.Message;
            }
        });
    }
}
```
- [ ] Implement console output with colors
- [ ] Create progress bar with Spectre.Console
- [ ] Handle MessageTarget filtering
- [ ] Format output nicely

## Phase 6: Integration & Testing ✅

### Checklist: Integration Tasks

#### Task: Create ClassicScanLogs Main Class
**File**: `Scanner111.Core/ClassicScanLogs.cs`
```csharp
namespace Scanner111.Core;

public class ClassicScanLogs
{
    private readonly ClassicScanLogsInfo _yamlData;
    private readonly List<string> _crashLogList;
    private readonly Dictionary<string, int> _crashLogStats;
    
    public bool? FcxMode { get; set; }
    public string? CustomFolder { get; set; }
    
    public ClassicScanLogs()
    {
        // Load configuration
        _yamlData = LoadConfiguration();
        _crashLogStats = new Dictionary<string, int>
        {
            ["scanned"] = 0,
            ["incomplete"] = 0,
            ["failed"] = 0
        };
    }
    
    public async Task CrashLogsScanAsync()
    {
        // Direct port of Python's crashlogs_scan()
        FCXModeHandler.ResetFcxChecks();
        
        // Get crash log files
        _crashLogList = await GetCrashLogFiles();
        
        if (_crashLogList.Count == 0)
        {
            MessageHandler.MsgWarning("No crash logs found to scan.");
            return;
        }
        
        MessageHandler.MsgInfo($"Found {_crashLogList.Count} crash logs to scan.");

        await ScanLogsAsync();
    }
}
```
- [ ] Port main scanning logic
- [ ] Implement file discovery
- [ ] Add async pipeline decision logic
- [ ] Handle statistics tracking

#### Task: Write Unit Tests
**File**: `Scanner111.Tests/Analyzers/FormIdAnalyzerTests.cs`
```csharp
[TestClass]
public class FormIdAnalyzerTests
{
    private FormIdAnalyzer _analyzer;
    private ClassicScanLogsInfo _config;
    
    [TestInitialize]
    public void Setup()
    {
        _config = new ClassicScanLogsInfo();
        _analyzer = new FormIdAnalyzer(_config, false, false);
    }
    
    [TestMethod]
    public async Task ExtractFormIds_ValidFormIds_ReturnsMatches()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            CallStack = new List<string>
            {
                "  Form ID: 0x0001A332",
                "  Form ID: 0xFF000000", // Should be skipped
                "  Form ID: 0x00014E45"
            }
        };
        
        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);
        var formIdResult = result as FormIdAnalysisResult;
        
        // Assert
        Assert.IsNotNull(formIdResult);
        Assert.AreEqual(2, formIdResult.FormIds.Count);
        Assert.IsTrue(formIdResult.FormIds.Contains("Form ID: 0001A332"));
        Assert.IsTrue(formIdResult.FormIds.Contains("Form ID: 00014E45"));
    }
}
```
- [ ] Create test project structure
- [ ] Add sample crash logs to test data
- [ ] Write tests for each analyzer
- [ ] Test orchestrator integration
- [ ] Test file I/O operations

### Checklist: Build & Deployment

#### Task: Configure Build
**File**: `Directory.Build.props`
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```
- [ ] Set up consistent build properties
- [ ] Configure nullable reference types
- [ ] Enable implicit usings
- [ ] Set up CI/CD if needed

#### Task: Create Publishing Profiles
- [ ] Windows x64 self-contained
- [ ] Windows x64 framework-dependent
- [ ] Linux x64 (if needed)
- [ ] macOS (if needed)

## Acceptance Criteria for Each Phase

### Phase 1 Complete When:
- [ ] All models compile without errors
- [ ] YamlSettingsCache can load and parse YAML files
- [ ] MessageHandler routes messages correctly
- [ ] GlobalRegistry stores and retrieves values
- [ ] Unit tests pass for infrastructure

### Phase 2 Complete When:
- [ ] All analyzers implement IAnalyzer interface
- [ ] FormID extraction matches Python output exactly
- [ ] Plugin matching works with ignore list
- [ ] All analyzers have unit tests
- [ ] Report formatting matches Python

### Phase 3 Complete When:
- [ ] Pipeline processes crash logs end-to-end with async/await
- [ ] Reports are generated correctly matching Python format
- [ ] Statistics are calculated properly
- [ ] Concurrent batch processing works efficiently
- [ ] Memory caching improves performance for repeated scans
- [ ] Can process sample crash logs with proper concurrency control

### Phase 4 Complete When:
- [ ] GUI launches and displays correctly
- [ ] All buttons and controls are functional
- [ ] Settings save and load properly
- [ ] Window is fully responsive at all sizes
- [ ] Progress dialogs work during scanning

### Phase 5 Complete When:
- [ ] CLI accepts all command-line arguments
- [ ] Console output is formatted nicely
- [ ] Progress bars display correctly
- [ ] Can run without GUI dependencies
- [ ] Exit codes are appropriate

### Phase 6 Complete When:
- [ ] All tests pass
- [ ] Can scan real crash logs
- [ ] Performance is acceptable
- [ ] No memory leaks
- [ ] Builds can be published

## Notes for AI Implementation

1. **Always check existing Python code** before implementing a feature
2. **Match output format exactly** - reports should be identical to Python version
3. **Use async/await properly** - ConfigureAwait(false) in library code
4. **Handle file encoding** - UTF-8 with ignore errors like Python
5. **Preserve all error messages** - users expect same messages
6. **Keep ClassicScanLogsInfo name** - it's referenced throughout the codebase
7. **Maintain "CLASSIC" in internal strings** - only change project/namespace names

## Common Pitfalls to Avoid

1. Don't change report formatting - it must match Python exactly
2. Don't skip error handling - port Python's try/except blocks
3. Don't forget cancellation tokens in async methods
4. Don't use blocking I/O in async methods
5. Don't forget to dispose resources (use `using` statements)
6. Keep internal references to "CLASSIC" in strings and messages

## Resources

- Python source: `/Code to Port/` directory
- Sample logs: `/sample_logs/` directory  
- YAML files: `/Code to Port/CLASSIC Data/databases/`
- Test data: Use actual crash logs for testing

## Project Name Mapping

- Solution: `Scanner111.sln`
- Core Library: `Scanner111.Core`
- GUI Application: `Scanner111.GUI`  
- CLI Application: `Scanner111.CLI`
- Test Project: `Scanner111.Tests`
- Window Title: "Scanner 111 - Crash Log Auto Scanner"
- CLI Display: "Scanner 111" in FigletText

Keep all other references (class names, YAML keys, report text) as "CLASSIC" to maintain compatibility with configuration files and user expectations.

## Project Name Mapping

- Solution: `Scanner111.sln`
- Core Library: `Scanner111.Core`
- GUI Application: `Scanner111.GUI`  
- CLI Application: `Scanner111.CLI`
- Test Project: `Scanner111.Tests`
- Window Title: "Scanner 111 - Crash Log Auto Scanner"
- CLI Display: "Scanner 111" in FigletText

Keep all other references (class names, YAML keys, report text) as "CLASSIC" to maintain compatibility with configuration files and user expectations.
