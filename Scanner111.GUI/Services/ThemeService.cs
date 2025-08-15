using Avalonia.Markup.Xaml;

namespace Scanner111.GUI.Services;

public interface IThemeService
{
    void SetTheme(string themeName);
    string GetCurrentTheme();
}

public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private string _currentTheme = "Dark";
    private ResourceDictionary? _currentThemeResources;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSavedTheme();
    }

    public void SetTheme(string themeName)
    {
        if (string.IsNullOrEmpty(themeName) || themeName == _currentTheme)
            return;

        try
        {
            var app = Application.Current;
            if (app == null) return;

            // Remove current theme resources if any
            if (_currentThemeResources != null && app.Resources.MergedDictionaries.Contains(_currentThemeResources))
                app.Resources.MergedDictionaries.Remove(_currentThemeResources);

            // Load new theme
            var themeUri = new Uri($"avares://Scanner111.GUI/Resources/Themes/{themeName}Theme.axaml");
            var themeResource = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);

            app.Resources.MergedDictionaries.Add(themeResource);
            _currentThemeResources = themeResource;
            _currentTheme = themeName;

            // Save theme preference
            SaveThemePreference(themeName);
        }
        catch (Exception ex)
        {
            // Log error or handle theme loading failure
            Console.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
        }
    }

    public string GetCurrentTheme()
    {
        return _currentTheme;
    }

    private void LoadSavedTheme()
    {
        try
        {
            var settings = _settingsService.LoadUserSettingsAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(settings.PreferredTheme))
                SetTheme(settings.PreferredTheme);
            else
                // Default to dark theme
                SetTheme("Dark");
        }
        catch
        {
            // If loading fails, use dark theme as default
            SetTheme("Dark");
        }
    }

    private void SaveThemePreference(string themeName)
    {
        try
        {
            var settings = _settingsService.LoadUserSettingsAsync().GetAwaiter().GetResult();
            settings.PreferredTheme = themeName;
            _settingsService.SaveUserSettingsAsync(settings).GetAwaiter().GetResult();
        }
        catch
        {
            // Silently fail if we can't save the preference
        }
    }
}