using Scanner111.Models;
using Scanner111.Services;
using Scanner111.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Scanner111.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
#if DEBUG
        public string Greeting => "Welcome to Avalonia, from MainWindowViewModel!";
#endif

        private readonly AppSettings _appSettings;
        private readonly ScanLogService? _scanLogService;
        private readonly CrashLogFormattingService? _formattingService;

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        private ObservableCollection<LogIssue> _scanResults = new();
        public ObservableCollection<LogIssue> ScanResults
        {
            get => _scanResults;
            set => this.RaiseAndSetIfChanged(ref _scanResults, value);
        }

        // Settings properties for UI binding
        public bool SimplifyLogs
        {
            get => _appSettings.SimplifyLogs;
            set
            {
                if (_appSettings.SimplifyLogs != value)
                {
                    _appSettings.SimplifyLogs = value;
                    this.RaisePropertyChanged(nameof(SimplifyLogs));
                }
            }
        }

        public bool PreserveOriginalFiles
        {
            get => _appSettings.PreserveOriginalFiles;
            set
            {
                if (_appSettings.PreserveOriginalFiles != value)
                {
                    _appSettings.PreserveOriginalFiles = value;
                    this.RaisePropertyChanged(nameof(PreserveOriginalFiles));
                }
            }
        }

        public bool AutoDetectCrashLogs
        {
            get => _appSettings.AutoDetectCrashLogs;
            set
            {
                if (_appSettings.AutoDetectCrashLogs != value)
                {
                    _appSettings.AutoDetectCrashLogs = value;
                    this.RaisePropertyChanged(nameof(AutoDetectCrashLogs));
                }
            }
        }        // Commands
        public ICommand ScanCrashLogsCommand { get; }
        public ICommand ReformatCrashLogsCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenPapyrusMonitoringCommand { get; }

        // Constructor for design-time, if needed, or for DI to inject services
        public MainWindowViewModel()
        {
            // This parameterless constructor can be used by the designer.
            // If AppSettings is critical even for design, you might initialize a default/mock instance here.
            // For runtime, the DI container will use the constructor that takes AppSettings.
            _appSettings = new AppSettings(); // Example: Provide a default for the designer or if DI fails            // Set up no-op commands for design-time
            ScanCrashLogsCommand = ReactiveCommand.Create(() => { });
            ReformatCrashLogsCommand = ReactiveCommand.Create(() => { });
            OpenSettingsCommand = ReactiveCommand.Create(() => { });
            OpenPapyrusMonitoringCommand = ReactiveCommand.Create(() => { });
        }

        public MainWindowViewModel(
            AppSettings appSettings,
            ScanLogService scanLogService,
            CrashLogFormattingService formattingService)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _scanLogService = scanLogService ?? throw new ArgumentNullException(nameof(scanLogService));
            _formattingService = formattingService ?? throw new ArgumentNullException(nameof(formattingService));            // Initialize commands
            ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync);
            ReformatCrashLogsCommand = ReactiveCommand.CreateFromTask(ReformatCrashLogsAsync);
            OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
            OpenPapyrusMonitoringCommand = ReactiveCommand.Create(OpenPapyrusMonitoring);
        }

        // Example property using a setting
        public string GamePathDisplay => $"Game Path from Settings: {_appSettings.GamePath}";

        /// <summary>
        /// Opens a file dialog to select crash log files and then scans them
        /// </summary>
        private async Task ScanCrashLogsAsync()
        {
            if (_scanLogService == null)
            {
                StatusMessage = "Error: ScanLogService is not available";
                return;
            }

            try
            {
                var files = await SelectCrashLogFilesAsync();
                if (files == null || !files.Any()) return;

                StatusMessage = "Scanning crash logs...";

                // Validate files if auto-detection is enabled
                if (_appSettings.AutoDetectCrashLogs && _formattingService != null)
                {
                    var validFiles = new List<string>();
                    foreach (var file in files)
                    {
                        if (await _formattingService.IsCrashLogAsync(file))
                        {
                            validFiles.Add(file);
                        }
                    }

                    if (validFiles.Count < files.Count())
                    {
                        StatusMessage = $"Warning: {files.Count() - validFiles.Count} files didn't appear to be crash logs and were skipped.";
                        files = validFiles;
                    }

                    if (!files.Any())
                    {
                        StatusMessage = "No valid crash log files were found. Please select crash log files.";
                        return;
                    }
                }

                // Create backups if enabled
                if (_appSettings.PreserveOriginalFiles)
                {
                    CreateBackups(files);
                }

                // First reformat the logs
                int processedCount = await _scanLogService.PreprocessCrashLogsAsync(files);

                // Then scan them
                var results = await _scanLogService.ScanMultipleLogFilesAsync(files);

                ScanResults.Clear();
                foreach (var result in results)
                {
                    ScanResults.Add(result);
                }

                // Show errors if any occurred during processing
                if (_appSettings.LastProcessingErrors.Any())
                {
                    StatusMessage = $"Scan complete with some issues. Processed {processedCount} files. Found {ScanResults.Count} issues. {_appSettings.LastProcessingErrors.Count} errors occurred during processing.";
                }
                else
                {
                    StatusMessage = $"Scan complete. Processed {processedCount} files. Found {ScanResults.Count} issues.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error scanning logs: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens a file dialog to select crash log files and reformats them
        /// </summary>
        private async Task ReformatCrashLogsAsync()
        {
            if (_formattingService == null)
            {
                StatusMessage = "Error: FormattingService is not available";
                return;
            }

            try
            {
                var files = await SelectCrashLogFilesAsync();
                if (files == null || !files.Any()) return;

                StatusMessage = "Reformatting crash logs...";

                // Validate files if auto-detection is enabled
                if (_appSettings.AutoDetectCrashLogs)
                {
                    var validFiles = new List<string>();
                    foreach (var file in files)
                    {
                        if (await _formattingService.IsCrashLogAsync(file))
                        {
                            validFiles.Add(file);
                        }
                    }

                    if (validFiles.Count < files.Count())
                    {
                        StatusMessage = $"Warning: {files.Count() - validFiles.Count} files didn't appear to be crash logs and were skipped.";
                        files = validFiles;
                    }

                    if (!files.Any())
                    {
                        StatusMessage = "No valid crash log files were found. Please select crash log files.";
                        return;
                    }
                }

                // Create backups if enabled
                if (_appSettings.PreserveOriginalFiles)
                {
                    CreateBackups(files);
                }

                int processedCount = await _formattingService.ReformatCrashLogsAsync(
                    files,
                    _appSettings.SimplifyRemoveStrings
                );

                // Show errors if any occurred during processing
                if (_appSettings.LastProcessingErrors.Any())
                {
                    StatusMessage = $"Reformat complete with some issues. Processed {processedCount} files. {_appSettings.LastProcessingErrors.Count} errors occurred during processing.";
                }
                else
                {
                    StatusMessage = $"Reformat complete. Processed {processedCount} files.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reformatting logs: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates backup copies of all files before modifying them
        /// </summary>
        private void CreateBackups(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                try
                {
                    string backupPath = file + ".bak";
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(file, backupPath, overwrite: false);
                    }
                }
                catch (Exception ex)
                {
                    _appSettings.LastProcessingErrors.Add($"Failed to create backup of {file}: {ex.Message}");
                }
            }
        }

        // Store a reference to the main window to use for dialogs
        private Window? _mainWindow;

        /// <summary>
        /// Set the main window reference for file dialogs
        /// </summary>
        public void SetMainWindow(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// Opens a file dialog to select multiple crash log files
        /// </summary>
        private async Task<IEnumerable<string>> SelectCrashLogFilesAsync()
        {
            try
            {
                // Since we may not have direct access to the TopLevel from the VM
                // we'll need to have the window reference set from the view
                if (_mainWindow == null)
                {
                    StatusMessage = "Error: Main window reference is not set";
                    return Array.Empty<string>();
                }

                // Create file types filter options
                var logFileTypes = new FilePickerFileType("Log Files")
                {
                    Patterns = new[] { "*.log", "*.txt" },
                    MimeTypes = new[] { "text/plain" }
                };

                var allFileTypes = new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                };

                // Configure open file dialog options
                var options = new FilePickerOpenOptions
                {
                    Title = "Select Crash Log Files",
                    AllowMultiple = true,
                    FileTypeFilter = new[] { logFileTypes, allFileTypes }
                };

                // Show the dialog using the StorageProvider from the window
                var result = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);

                // Convert IStorageFile results to file paths
                return result?.Select(file => file.Path.LocalPath) ?? Array.Empty<string>();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error selecting files: {ex.Message}";
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Opens the settings dialog/window
        /// </summary>
        private void OpenSettings()
        {
            // Not yet implemented
            StatusMessage = "Settings dialog not yet implemented";
        }

        /// <summary>
        /// Opens the Papyrus log monitoring window
        /// </summary>
        private void OpenPapyrusMonitoring()
        {
            // Get the service provider from the application
            var app = Avalonia.Application.Current as App;
            if (app?._serviceProvider == null || _mainWindow == null)
            {
                StatusMessage = "Could not open Papyrus monitoring window";
                return;
            }

            // Create a new PapyrusMonitoringView with its ViewModel from DI
            var papyrusMonitoringViewModel = app._serviceProvider.GetRequiredService<PapyrusMonitoringViewModel>();
            var papyrusMonitoringView = new PapyrusMonitoringView
            {
                DataContext = papyrusMonitoringViewModel
            };

            // Show the dialog
            papyrusMonitoringView.ShowDialog(_mainWindow);

            StatusMessage = "Opened Papyrus monitoring window";
        }
    }
}
