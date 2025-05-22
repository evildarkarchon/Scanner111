using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Services;
using Scanner111.ViewModels;
using Scanner111.Views;

namespace Scanner111;

public partial class App : Application
{
    /// <summary>
    /// Gets or sets the application's service provider.
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
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

        base.OnFrameworkInitializationCompleted();
    }
}
