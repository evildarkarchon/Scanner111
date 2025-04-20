using System;
using System.Collections.Generic;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Core.Interfaces.Services;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IPluginSystemService _pluginSystemService;
    private readonly IFileSystemService _fileSystemService;
    
    private bool _isLoading = true;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private ObservableCollection<PluginInfoViewModel> _plugins = [];
    
    // Application Settings
    private string _crashLogsPath = string.Empty;
    private string _moveUnsolvedLogsTo = string.Empty;
    private bool _moveUnsolvedLogs;
    private bool _simplifyLogs;
    private bool _showFormIdValues;
    private bool _useDarkTheme = true;
    private bool _checkForUpdatesOnStartup = true;
    private string _currentLanguage = "English";
    private ObservableCollection<string> _availableLanguages = [];
    
    public SettingsViewModel(
        IConfigurationService configurationService,
        IPluginSystemService pluginSystemService,
        IFileSystemService fileSystemService)
    {
        _configurationService = configurationService;
        _pluginSystemService = pluginSystemService;
        _fileSystemService = fileSystemService;
        
        SaveSettingsCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        ResetSettingsCommand = ReactiveCommand.CreateFromTask(ResetSettingsAsync);
        SelectCrashLogsPathCommand = ReactiveCommand.CreateFromTask<string>(SelectCrashLogsPathAsync);
        SelectMoveUnsolvedLogsPathCommand = ReactiveCommand.CreateFromTask<string>(SelectMoveUnsolvedLogsPathAsync);
        RefreshPluginsCommand = ReactiveCommand.CreateFromTask(LoadPluginsAsync);
        TogglePluginStateCommand = ReactiveCommand.Create<PluginInfoViewModel>(TogglePluginState);
        
        // Load settings asynchronously
        LoadSettingsAsync().ConfigureAwait(false);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }
    
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }
    
    public ObservableCollection<PluginInfoViewModel> Plugins
    {
        get => _plugins;
        set => this.RaiseAndSetIfChanged(ref _plugins, value);
    }
    
    public string CrashLogsPath
    {
        get => _crashLogsPath;
        set => this.RaiseAndSetIfChanged(ref _crashLogsPath, value);
    }
    
    public string MoveUnsolvedLogsTo
    {
        get => _moveUnsolvedLogsTo;
        set => this.RaiseAndSetIfChanged(ref _moveUnsolvedLogsTo, value);
    }
    
    public bool MoveUnsolvedLogs
    {
        get => _moveUnsolvedLogs;
        set => this.RaiseAndSetIfChanged(ref _moveUnsolvedLogs, value);
    }
    
    public bool SimplifyLogs
    {
        get => _simplifyLogs;
        set => this.RaiseAndSetIfChanged(ref _simplifyLogs, value);
    }
    
    public bool ShowFormIdValues
    {
        get => _showFormIdValues;
        set => this.RaiseAndSetIfChanged(ref _showFormIdValues, value);
    }
    
    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set => this.RaiseAndSetIfChanged(ref _useDarkTheme, value);
    }
    
    public bool CheckForUpdatesOnStartup
    {
        get => _checkForUpdatesOnStartup;
        set => this.RaiseAndSetIfChanged(ref _checkForUpdatesOnStartup, value);
    }
    
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => this.RaiseAndSetIfChanged(ref _currentLanguage, value);
    }
    
    public ObservableCollection<string> AvailableLanguages
    {
        get => _availableLanguages;
        set => this.RaiseAndSetIfChanged(ref _availableLanguages, value);
    }
    
    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetSettingsCommand { get; }
    public ReactiveCommand<string, Unit> SelectCrashLogsPathCommand { get; }
    public ReactiveCommand<string, Unit> SelectMoveUnsolvedLogsPathCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPluginsCommand { get; }
    public ReactiveCommand<PluginInfoViewModel, Unit> TogglePluginStateCommand { get; }
    
    private async Task LoadSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading settings...";
        
        try
        {
            // Load application settings
            var appSettings = await _configurationService.LoadConfigurationAsync<AppSettings>("AppSettings");
            if (appSettings != null)
            {
                CrashLogsPath = appSettings.CrashLogsPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash Logs");
                MoveUnsolvedLogsTo = appSettings.MoveUnsolvedLogsTo ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unsolved Logs");
                MoveUnsolvedLogs = appSettings.MoveUnsolvedLogs;
                SimplifyLogs = appSettings.SimplifyLogs;
                ShowFormIdValues = appSettings.ShowFormIdValues;
                UseDarkTheme = appSettings.UseDarkTheme;
                CheckForUpdatesOnStartup = appSettings.CheckForUpdatesOnStartup;
                CurrentLanguage = appSettings.Language ?? "English";
            }
            else
            {
                // Set default values
                CrashLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash Logs");
                MoveUnsolvedLogsTo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unsolved Logs");
                MoveUnsolvedLogs = false;
                SimplifyLogs = true;
                ShowFormIdValues = false;
                UseDarkTheme = true;
                CheckForUpdatesOnStartup = true;
                CurrentLanguage = "English";
            }
            
            // Load available languages
            AvailableLanguages = new ObservableCollection<string>(GetAvailableLanguages());
            
            // Load plugins
            await LoadPluginsAsync();
            
            StatusMessage = "Settings loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }
    }
    
    private async Task LoadPluginsAsync()
    {
        try
        {
            var plugins = await _pluginSystemService.DiscoverPluginsAsync();
            
            var pluginViewModels = plugins.Select(plugin => new PluginInfoViewModel
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Description = plugin.Description,
                    Version = plugin.Version,
                    IsEnabled = true,
                    SupportedGames = string.Join(", ", plugin.SupportedGameIds)
                })
                .ToList();

            Plugins = new ObservableCollection<PluginInfoViewModel>(pluginViewModels);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading plugins: {ex.Message}";
        }
    }
    
    private void TogglePluginState(PluginInfoViewModel plugin)
    {
        plugin.IsEnabled = !plugin.IsEnabled;
        
        // In a real application, this would update the plugin state
        // await _pluginSystemService.EnablePlugin(plugin.Id, plugin.IsEnabled);
    }
    
    private async Task SaveSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving settings...";
        
        try
        {
            // Create settings object
            var appSettings = new AppSettings
            {
                CrashLogsPath = CrashLogsPath,
                MoveUnsolvedLogsTo = MoveUnsolvedLogsTo,
                MoveUnsolvedLogs = MoveUnsolvedLogs,
                SimplifyLogs = SimplifyLogs,
                ShowFormIdValues = ShowFormIdValues,
                UseDarkTheme = UseDarkTheme,
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                Language = CurrentLanguage
            };
            
            // Save settings
            await _configurationService.SaveConfigurationAsync("AppSettings", appSettings);
            
            // Apply theme change if needed - this would be handled by the application's theme service
            // Apply language change if needed - this would be handled by the application's localization service
            
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ResetSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Resetting settings to default...";
        
        try
        {
            // Reset to default values
            CrashLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash Logs");
            MoveUnsolvedLogsTo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unsolved Logs");
            MoveUnsolvedLogs = false;
            SimplifyLogs = true;
            ShowFormIdValues = false;
            UseDarkTheme = true;
            CheckForUpdatesOnStartup = true;
            CurrentLanguage = "English";
            
            // Save default settings
            await SaveSettingsAsync();
            
            StatusMessage = "Settings reset to default values.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task SelectCrashLogsPathAsync(string path)
    {
        if (Directory.Exists(path))
        {
            CrashLogsPath = path;
        }
    }
    
    private async Task SelectMoveUnsolvedLogsPathAsync(string path)
    {
        if (Directory.Exists(path))
        {
            MoveUnsolvedLogsTo = path;
        }
    }
    
    private IEnumerable<string> GetAvailableLanguages()
    {
        // In a real application, this would scan for available language files
        return ["English", "German", "French", "Spanish", "Russian"];
    }
}

public class PluginInfoViewModel : ViewModelBase
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _version = string.Empty;
    private bool _isEnabled;
    private string _supportedGames = string.Empty;
    
    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }
    
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }
    
    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }
    
    public string Version
    {
        get => _version;
        set => this.RaiseAndSetIfChanged(ref _version, value);
    }
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }
    
    public string SupportedGames
    {
        get => _supportedGames;
        set => this.RaiseAndSetIfChanged(ref _supportedGames, value);
    }
}

public class AppSettings
{
    public string? CrashLogsPath { get; set; }
    public string? MoveUnsolvedLogsTo { get; set; }
    public bool MoveUnsolvedLogs { get; set; }
    public bool SimplifyLogs { get; set; }
    public bool ShowFormIdValues { get; set; }
    public bool UseDarkTheme { get; set; }
    public bool CheckForUpdatesOnStartup { get; set; }
    public string? Language { get; set; }
}