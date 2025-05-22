// filepath: c:\Users\evild\RiderProjects\Scanner111\Scanner111\App.axaml.cs

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.ViewModels;
using Scanner111.Views;
using System;

namespace Scanner111;

public partial class App : Application
{
    // Field to store the service provider when the DI constructor is called.
    internal IServiceProvider? _serviceProvider;

    // Parameterless constructor for XAML
    public App()
    {
        // Empty constructor - initialization done in Initialize()
    }

    // Constructor to be called from Program.cs with the ServiceProvider
    public App(IServiceProvider serviceProvider) : this()
    {
        _serviceProvider = serviceProvider;
        Console.WriteLine("App constructor with service provider called");
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
        Console.WriteLine("App initialized");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            Console.WriteLine("Framework initialization completed");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (_serviceProvider != null)
                {
                    try
                    {
                        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                        desktop.MainWindow = new MainWindow
                        {
                            DataContext = viewModel
                        };
                        Console.WriteLine("MainWindow created with ViewModel from DI");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating MainWindow: {ex.Message}");
                        throw;
                    }
                }
                else if (Avalonia.Controls.Design.IsDesignMode)
                {
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainWindowViewModel()
                    };
                    Console.WriteLine("MainWindow created for design mode");
                }
                else
                {
                    Console.WriteLine("Error: No service provider available");
                    throw new InvalidOperationException("No service provider available");
                }
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                if (_serviceProvider != null)
                {
                    singleView.MainView = new MainWindow
                    {
                        DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
                    };
                    Console.WriteLine("MainView set for SingleViewApplicationLifetime");
                }
                else if (Avalonia.Controls.Design.IsDesignMode)
                {
                    singleView.MainView = new MainWindow
                    {
                        DataContext = new MainWindowViewModel()
                    };
                    Console.WriteLine("MainView set for design mode");
                }
                else
                {
                    Console.WriteLine("Error: No service provider available for SingleView");
                    throw new InvalidOperationException("No service provider available for SingleView");
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error in OnFrameworkInitializationCompleted: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
