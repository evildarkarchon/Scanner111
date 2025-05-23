using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Scanner111.ViewModels;
using Scanner111.Views;

namespace Scanner111;

public class App : Application
{
    /// <summary>
    ///     Gets or sets the application's service provider.
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    /*public override void OnFrameworkInitializationCompleted()
    {
        if (ServiceProvider != null)
        {
            // The YamlSettingsCache singleton is already initialized when fetched from the DI container
            var yamlSettingsCache = ServiceProvider.GetRequiredService<IYamlSettingsCache>();

            // Still initialize YamlSettings for backward compatibility
            YamlSettings.Initialize(yamlSettingsCache);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (ServiceProvider != null)
            {
                // Create MainViewModel with DI
                var mainViewModel = ActivatorUtilities.CreateInstance<MainViewModel>(ServiceProvider);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
            }
            else
            {
                // Fallback for design-time or when DI is not set up
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            if (ServiceProvider != null)
            {
                // Create MainViewModel with DI
                var mainViewModel = ActivatorUtilities.CreateInstance<MainViewModel>(ServiceProvider);

                singleViewPlatform.MainView = new MainView
                {
                    DataContext = mainViewModel
                };
            }
            else
            {
                // Fallback for design-time or when DI is not set up
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
            }
        }
    }*/
}