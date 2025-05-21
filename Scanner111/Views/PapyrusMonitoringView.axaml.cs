using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scanner111.ViewModels;

namespace Scanner111.Views
{
    public partial class PapyrusMonitoringView : Window
    {
        public PapyrusMonitoringView()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            // When the window is closed, we need to stop monitoring
            if (DataContext is PapyrusMonitoringViewModel vm)
            {
                vm.StopMonitoring();
            }

            base.OnClosed(e);
        }
    }
}
