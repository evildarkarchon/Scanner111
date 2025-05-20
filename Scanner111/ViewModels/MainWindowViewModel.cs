using Scanner111.Models; // Added for AppSettings
using System; // Added for ArgumentNullException

namespace Scanner111.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
#if DEBUG
        public string Greeting => "Welcome to Avalonia, from MainWindowViewModel!";
#endif

        private readonly AppSettings _appSettings;

        // Constructor for design-time, if needed, or for DI to inject services
        public MainWindowViewModel()
        {
            // This parameterless constructor can be used by the designer.
            // If AppSettings is critical even for design, you might initialize a default/mock instance here.
            // For runtime, the DI container will use the constructor that takes AppSettings.
            _appSettings = new AppSettings(); // Example: Provide a default for the designer or if DI fails
        }

        public MainWindowViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            // You can now use _appSettings to access application settings
            // For example: string gamePath = _appSettings.GamePath;
        }

        // Example property using a setting
        public string GamePathDisplay => $"Game Path from Settings: {_appSettings.GamePath}";
    }
}
