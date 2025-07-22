using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Scanner111.GUI.Views;

namespace Scanner111.GUI;

public class App : Application
{
    /// <summary>
    /// Initializes the Avalonia application by loading its XAML resources.
    /// </summary>
    /// <remarks>
    /// This method is overridden from the base <see cref="Application"/> class to
    /// load the XAML resources associated with the application. It ensures that
    /// all styles, templates, and other UI components defined in XAML are properly
    /// initialized before the application starts running.
    /// </remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Called when the framework has completed its initialization process.
    /// </summary>
    /// <remarks>
    /// This method is overridden to provide additional setup for the application's
    /// startup sequence. Specifically, it ensures that the main window is created
    /// and assigned to the application's lifetime. If the application is configured
    /// to use a <see cref="IClassicDesktopStyleApplicationLifetime"/>, the main window
    /// will be set to an instance of <c>MainWindow</c>.
    /// After performing the custom initialization, the method calls the base implementation
    /// to complete the default framework setup process.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}