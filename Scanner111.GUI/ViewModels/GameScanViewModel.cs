using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.GUI.Services;

namespace Scanner111.GUI.ViewModels;

public class GameScanViewModel : ViewModelBase
{
    private readonly IGameScannerService _gameScannerService;
    private readonly IMessageHandler _messageHandler;
    private readonly ISettingsService _settingsService;
    private string _crashGenStatus = "";
    private ObservableCollection<string> _criticalIssues = new();

    private string _gameInstallPath = "";
    private bool _hasGamePath;
    private bool _hasResults;
    private bool _isScanning;
    private string _modIniStatus = "";
    private string _progressText = "";
    private double _progressValue;
    private bool _progressVisible;
    private CancellationTokenSource? _scanCancellationTokenSource;
    private GameScanResult? _scanResult;
    private GameType _selectedGameType = GameType.Fallout4;
    private ObservableCollection<string> _warnings = new();
    private string _wryeBashStatus = "";
    private string _xsePluginStatus = "";

    public GameScanViewModel(
        IGameScannerService gameScannerService,
        IMessageHandler messageHandler,
        ISettingsService settingsService)
    {
        _gameScannerService = gameScannerService ?? throw new ArgumentNullException(nameof(gameScannerService));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        SelectGamePathCommand = ReactiveCommand.CreateFromTask(SelectGamePath);
        StartScanCommand = ReactiveCommand.CreateFromTask(
            StartGameScan,
            this.WhenAnyValue(x => x.HasGamePath, x => x.IsScanning,
                (hasPath, scanning) => hasPath && !scanning));
        CancelScanCommand = ReactiveCommand.Create(CancelScan);
        ClearResultsCommand = ReactiveCommand.Create(ClearResults);
        ExportReportCommand = ReactiveCommand.CreateFromTask(
            ExportReport,
            this.WhenAnyValue(x => x.HasResults));

        RunIndividualScanCommand = ReactiveCommand.CreateFromTask<string>(
            RunIndividualScan,
            this.WhenAnyValue(x => x.HasGamePath, x => x.IsScanning,
                (hasPath, scanning) => hasPath && !scanning));

        _ = InitializeAsync();
    }

    public string GameInstallPath
    {
        get => _gameInstallPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _gameInstallPath, value);
            HasGamePath = !string.IsNullOrWhiteSpace(value);
        }
    }

    public GameType SelectedGameType
    {
        get => _selectedGameType;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedGameType, value);
            _ = AutoDetectGamePath();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    public bool HasGamePath
    {
        get => _hasGamePath;
        private set => this.RaiseAndSetIfChanged(ref _hasGamePath, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => this.RaiseAndSetIfChanged(ref _progressText, value);
    }

    public bool ProgressVisible
    {
        get => _progressVisible;
        set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
    }

    public GameScanResult? ScanResult
    {
        get => _scanResult;
        set
        {
            this.RaiseAndSetIfChanged(ref _scanResult, value);
            HasResults = value != null;
            UpdateResultsDisplay();
        }
    }

    public string CrashGenStatus
    {
        get => _crashGenStatus;
        set => this.RaiseAndSetIfChanged(ref _crashGenStatus, value);
    }

    public string XsePluginStatus
    {
        get => _xsePluginStatus;
        set => this.RaiseAndSetIfChanged(ref _xsePluginStatus, value);
    }

    public string ModIniStatus
    {
        get => _modIniStatus;
        set => this.RaiseAndSetIfChanged(ref _modIniStatus, value);
    }

    public string WryeBashStatus
    {
        get => _wryeBashStatus;
        set => this.RaiseAndSetIfChanged(ref _wryeBashStatus, value);
    }

    public ObservableCollection<string> CriticalIssues
    {
        get => _criticalIssues;
        set => this.RaiseAndSetIfChanged(ref _criticalIssues, value);
    }

    public ObservableCollection<string> Warnings
    {
        get => _warnings;
        set => this.RaiseAndSetIfChanged(ref _warnings, value);
    }

    public bool HasResults
    {
        get => _hasResults;
        private set => this.RaiseAndSetIfChanged(ref _hasResults, value);
    }

    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }
    public ReactiveCommand<Unit, Unit> StartScanCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportReportCommand { get; }
    public ReactiveCommand<string, Unit> RunIndividualScanCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsService.LoadUserSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.DefaultGamePath))
                GameInstallPath = settings.DefaultGamePath;
            else
                await AutoDetectGamePath();
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to initialize: {ex.Message}");
        }
    }

    private async Task SelectGamePath()
    {
        try
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow?.StorageProvider != null)
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = $"Select {SelectedGameType} Installation Directory",
                    AllowMultiple = false
                };

                var results = await mainWindow.StorageProvider.OpenFolderPickerAsync(options);
                var folder = results?.FirstOrDefault();

                if (folder != null)
                {
                    var path = folder.Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        GameInstallPath = path;

                        var settings = await _settingsService.LoadUserSettingsAsync();
                        settings.DefaultGamePath = path;
                        await _settingsService.SaveUserSettingsAsync(settings);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to select game path: {ex.Message}");
        }
    }

    private async Task AutoDetectGamePath()
    {
        try
        {
            var commonPaths = SelectedGameType == GameType.Fallout4
                ? new[]
                {
                    @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4",
                    @"C:\Program Files\Steam\steamapps\common\Fallout 4",
                    @"C:\Games\Fallout 4",
                    @"D:\Steam\steamapps\common\Fallout 4",
                    @"D:\Games\Fallout 4"
                }
                : new[]
                {
                    @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition",
                    @"C:\Program Files\Steam\steamapps\common\Skyrim Special Edition",
                    @"C:\Games\Skyrim Special Edition",
                    @"D:\Steam\steamapps\common\Skyrim Special Edition",
                    @"D:\Games\Skyrim Special Edition"
                };

            foreach (var path in commonPaths)
                if (Directory.Exists(path))
                {
                    GameInstallPath = path;
                    _messageHandler.ShowInfo($"Auto-detected {SelectedGameType} at: {path}");
                    break;
                }

            if (string.IsNullOrWhiteSpace(GameInstallPath))
                _messageHandler.ShowWarning(
                    $"Could not auto-detect {SelectedGameType} installation. Please select manually.");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Auto-detection failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task StartGameScan()
    {
        try
        {
            IsScanning = true;
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Starting comprehensive game scan...";
            ClearResults();

            _scanCancellationTokenSource = new CancellationTokenSource();

            UpdateProgress(10, "Checking Crash Generator configuration...");
            await Task.Delay(200);

            UpdateProgress(25, "Validating XSE plugins...");
            await Task.Delay(200);

            UpdateProgress(50, "Scanning mod INI files...");
            await Task.Delay(200);

            UpdateProgress(75, "Analyzing Wrye Bash report...");
            await Task.Delay(200);

            var result = await _gameScannerService.ScanGameAsync(_scanCancellationTokenSource.Token);

            UpdateProgress(100, "Scan complete!");
            ScanResult = result;

            _messageHandler.ShowSuccess("Game scan completed successfully!");
        }
        catch (OperationCanceledException)
        {
            _messageHandler.ShowWarning("Game scan was cancelled.");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Game scan failed: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    private async Task RunIndividualScan(string scanType)
    {
        try
        {
            IsScanning = true;
            ProgressVisible = true;
            ProgressValue = 0;

            _scanCancellationTokenSource = new CancellationTokenSource();
            var result = "";

            switch (scanType)
            {
                case "CrashGen":
                    ProgressText = "Checking Crash Generator configuration...";
                    result = await _gameScannerService.CheckCrashGenAsync(_scanCancellationTokenSource.Token);
                    CrashGenStatus = result;
                    break;

                case "XsePlugins":
                    ProgressText = "Validating XSE plugins...";
                    result = await _gameScannerService.ValidateXsePluginsAsync(_scanCancellationTokenSource.Token);
                    XsePluginStatus = result;
                    break;

                case "ModInis":
                    ProgressText = "Scanning mod INI files...";
                    result = await _gameScannerService.ScanModInisAsync(_scanCancellationTokenSource.Token);
                    ModIniStatus = result;
                    break;

                case "WryeBash":
                    ProgressText = "Analyzing Wrye Bash report...";
                    result = await _gameScannerService.CheckWryeBashAsync(_scanCancellationTokenSource.Token);
                    WryeBashStatus = result;
                    break;
            }

            UpdateProgress(100, $"{scanType} scan complete!");
            _messageHandler.ShowSuccess($"{scanType} scan completed successfully!");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"{scanType} scan failed: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
    }

    private void ClearResults()
    {
        ScanResult = null;
        CrashGenStatus = "";
        XsePluginStatus = "";
        ModIniStatus = "";
        WryeBashStatus = "";
        CriticalIssues.Clear();
        Warnings.Clear();
    }

    private void UpdateResultsDisplay()
    {
        if (ScanResult == null) return;

        CrashGenStatus = ScanResult.CrashGenResults;
        XsePluginStatus = ScanResult.XsePluginResults;
        ModIniStatus = ScanResult.ModIniResults;
        WryeBashStatus = ScanResult.WryeBashResults;

        CriticalIssues.Clear();
        foreach (var issue in ScanResult.CriticalIssues) CriticalIssues.Add(issue);

        Warnings.Clear();
        foreach (var warning in ScanResult.Warnings) Warnings.Add(warning);
    }

    private async Task ExportReport()
    {
        try
        {
            if (ScanResult == null) return;

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow?.StorageProvider != null)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = "Export Game Scan Report",
                    DefaultExtension = "md",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Markdown Files") { Patterns = new[] { "*.md" } },
                        new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    },
                    SuggestedFileName = $"GameScan_{DateTime.Now:yyyyMMdd_HHmmss}.md"
                };

                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(options);
                if (file != null)
                {
                    var path = file.Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        await File.WriteAllTextAsync(path, ScanResult.GetFullReport());
                        _messageHandler.ShowSuccess($"Report exported to: {path}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to export report: {ex.Message}");
        }
    }

    private void UpdateProgress(double value, string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ProgressValue = value;
            ProgressText = text;
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressValue = value;
                ProgressText = text;
            });
        }
    }
}