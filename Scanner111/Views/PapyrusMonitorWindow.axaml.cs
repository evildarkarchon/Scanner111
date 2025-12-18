using System;
using Avalonia.ReactiveUI;
using Scanner111.ViewModels;

namespace Scanner111.Views;

/// <summary>
/// Window for displaying Papyrus log monitoring statistics.
/// </summary>
public partial class PapyrusMonitorWindow : ReactiveWindow<PapyrusMonitorViewModel>
{
    public PapyrusMonitorWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is PapyrusMonitorViewModel vm)
        {
            vm.StopRequested += OnStopRequested;
        }
    }

    private void OnStopRequested(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Ensure monitoring stops and resources are cleaned up when window is closed
        if (DataContext is PapyrusMonitorViewModel vm)
        {
            vm.StopRequested -= OnStopRequested;
            vm.Dispose();
        }
    }
}
