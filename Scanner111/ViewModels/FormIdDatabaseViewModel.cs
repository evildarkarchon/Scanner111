using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using Scanner111.Models;
using Scanner111.Services;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Scanner111.ViewModels
{
    public class FormIdDatabaseViewModel : ViewModelBase
    {
        private readonly AppSettings _appSettings;
        private readonly FormIdDatabaseService _formIdDatabaseService;
        private readonly FormIdDatabaseImporter _databaseImporter;

        private string _statusMessage = string.Empty;
        private bool _isImporting;
        private int _importProgress;
        private bool _databaseExists;
        private string _selectedImportDirectory = string.Empty;

        public FormIdDatabaseViewModel(
            AppSettings appSettings,
            FormIdDatabaseService formIdDatabaseService,
            FormIdDatabaseImporter databaseImporter)
        {
            _appSettings = appSettings;
            _formIdDatabaseService = formIdDatabaseService;
            _databaseImporter = databaseImporter;

            // Set initial state
            DatabasePath = _appSettings.FormIdDatabasePath ?? string.Empty;
            DatabaseExists = _formIdDatabaseService.DatabaseExists();
            _showFormIdValues = _appSettings.ShowFormIdValues;

            // Commands
            ImportDatabaseCommand = ReactiveCommand.CreateFromTask<string>(ImportDatabase);
            ShowDatabaseDetailsCommand = ReactiveCommand.Create(ShowDatabaseDetails);
            BrowseForImportDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseForImportDirectory);

            // Update database status when ShowFormIdValues changes
            this.WhenAnyValue(x => x.ShowFormIdValues)
                .Subscribe(value => _appSettings.ShowFormIdValues = value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IsImporting
        {
            get => _isImporting;
            set => this.RaiseAndSetIfChanged(ref _isImporting, value);
        }

        public int ImportProgress
        {
            get => _importProgress;
            set => this.RaiseAndSetIfChanged(ref _importProgress, value);
        }

        public bool DatabaseExists
        {
            get => _databaseExists;
            set => this.RaiseAndSetIfChanged(ref _databaseExists, value);
        }

        public string DatabasePath { get; private set; }
        private bool _showFormIdValues;

        public bool ShowFormIdValues
        {
            get => _showFormIdValues;
            set
            {
                this.RaiseAndSetIfChanged(ref _showFormIdValues, value);
                _appSettings.ShowFormIdValues = value;
            }
        }

        public string SelectedImportDirectory
        {
            get => _selectedImportDirectory;
            set => this.RaiseAndSetIfChanged(ref _selectedImportDirectory, value);
        }

        public ICommand ImportDatabaseCommand { get; }
        public ICommand ShowDatabaseDetailsCommand { get; }
        public ICommand BrowseForImportDirectoryCommand { get; }

        private async Task ImportDatabase(string csvDirectoryPath)
        {
            if (string.IsNullOrEmpty(csvDirectoryPath))
            {
                StatusMessage = "Please select a directory containing CSV files";
                return;
            }

            try
            {
                IsImporting = true;
                ImportProgress = 0;
                StatusMessage = "Importing FormID database...";

                // Create target path if it doesn't exist
                var databaseFileName = $"{_appSettings.GameName}_FormIDs.db";
                var targetDbPath = System.IO.Path.Combine(
                    _appSettings.LocalDir,
                    "CLASSIC Data",
                    "databases",
                    databaseFileName);

                var progress = new Progress<int>(value =>
                {
                    // Update progress on UI thread
                    Dispatcher.UIThread.Post(() => ImportProgress = value);
                });

                var result = await _databaseImporter.ImportFromDirectory(
                    csvDirectoryPath, targetDbPath, progress);

                if (result.Success)
                {
                    StatusMessage = $"Import completed: {result.RecordsImported} records imported";
                    DatabasePath = targetDbPath;
                    DatabaseExists = true;
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during import: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 100;
            }
        }

        private void ShowDatabaseDetails()
        {
            if (!_formIdDatabaseService.DatabaseExists())
            {
                StatusMessage = "FormID database not found or not accessible";
                return;
            }

            StatusMessage = $"Using FormID database: {_appSettings.FormIdDatabasePath}";
        }
        private async Task BrowseForImportDirectory()
        {
            try
            {
                // Check if we have access to Avalonia's TopLevel storage provider
                var desktop = Avalonia.Application.Current?.ApplicationLifetime as
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var topLevel = desktop?.MainWindow;

                if (topLevel != null)
                {
                    var folderDialog = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select CSV Directory",
                        AllowMultiple = false
                    });

                    if (folderDialog != null && folderDialog.Count > 0)
                    {
                        var folder = folderDialog[0];
                        SelectedImportDirectory = folder.Path.LocalPath;

                        // Check if the directory has importable files
                        if (_databaseImporter.HasImportableFiles(SelectedImportDirectory))
                        {
                            StatusMessage = $"Found CSV files in {Path.GetFileName(SelectedImportDirectory)}. Click 'Import CSV Files' to proceed.";
                        }
                        else
                        {
                            StatusMessage = "No CSV files found in the selected directory.";
                        }
                    }
                }
                else
                {
                    StatusMessage = "Unable to open folder dialog. Please check your application configuration.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error browsing for directory: {ex.Message}";
            }
        }
    }
}
