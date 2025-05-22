// filepath: c:\Users\evild\RiderProjects\Scanner111\Scanner111\App.axaml.cs

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.ViewModels;
using Scanner111.Views;

namespace Scanner111;

public class App : Application
{
    // Field to store the service provider when the DI constructor is called.
    internal IServiceProvider? ServiceProvider;

    // Parameterless constructor for XAML
    public App()
    {
        // Empty constructor - initialization done in Initialize()
    }

    // Constructor to be called from Program.cs with the ServiceProvider
    public App(IServiceProvider serviceProvider) : this()
    {
        ServiceProvider = serviceProvider;
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
                if (ServiceProvider != null)
                {
                    try
                    {
                        var viewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();
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
                else if (Design.IsDesignMode)
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
                if (ServiceProvider != null)
                {
                    singleView.MainView = new MainWindow
                    {
                        DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>()
                    };
                    Console.WriteLine("MainView set for SingleViewApplicationLifetime");
                }
                else if (Design.IsDesignMode)
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