using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.ViewModels;
using Scanner111.Views;
using System;
// For Design.IsDesignMode, Avalonia typically uses a property on Application or Control.
// Let's ensure we are using the correct way to check for design mode.

namespace Scanner111;

public partial class App : Application
{
    // Parameterless constructor for XAML and potentially the designer
    public App()
    {
        // InitializeComponent(); // This is called by AvaloniaXamlLoader.Load(this)
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this); // Loads XAML and calls InitializeComponent for the App class itself
        base.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Access the ServiceProvider from a static property or a DI container specific to Avalonia if available.
        // For Microsoft.Extensions.DependencyInjection, it's usually passed in or built here.
        // We configured it in Program.cs, so it should be passed to the App constructor.
        // However, the App class itself might be instantiated by Avalonia without the DI constructor for the designer.

        IServiceProvider? serviceProvider = null;
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // Attempt to get the service provider if it was stored, e.g. in a static field or passed differently.
            // This example assumes Program.cs correctly passes it to a constructor that sets a field.
            // If `Current` is an `App` instance that had its DI constructor called:
            if (Application.Current is App appWithServices && appWithServices._serviceProvider != null)
            {
                serviceProvider = appWithServices._serviceProvider;
            }

            if (serviceProvider != null)
            {
                desktopLifetime.MainWindow = new MainWindow
                {
                    DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>()
                };
            }
            else if (Avalonia.Controls.Design.IsDesignMode) // Correct way to check for design mode
            {
                // Design mode: Create a ViewModel instance directly (requires parameterless constructor)
                desktopLifetime.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel() // Ensure MainWindowViewModel has a parameterless constructor for the designer
                };
            }
            else
            {
                throw new InvalidOperationException("ServiceProvider is not available and not in Design Mode.");
            }
        }
        else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            if (Application.Current is App appWithServices && appWithServices._serviceProvider != null)
            {
                serviceProvider = appWithServices._serviceProvider;
            }

            if (serviceProvider != null)
            {
                singleViewPlatform.MainView = new MainWindow
                {
                    DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>()
                };
            }
            else if (Avalonia.Controls.Design.IsDesignMode)
            {
                singleViewPlatform.MainView = new MainWindow
                {
                    DataContext = new MainWindowViewModel() // Ensure MainWindowViewModel has a parameterless constructor
                };
            }
            else
            {
                throw new InvalidOperationException("ServiceProvider is not available for SingleView and not in Design Mode.");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Field to store the service provider when the DI constructor is called.
    private IServiceProvider? _serviceProvider;

    // Constructor to be called from Program.cs with the ServiceProvider
    public App(IServiceProvider serviceProvider) : this() // Calls the parameterless constructor first
    {
        _serviceProvider = serviceProvider;
    }
}