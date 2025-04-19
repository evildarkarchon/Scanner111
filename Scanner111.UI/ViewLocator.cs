// Scanner111.UI/ViewLocator.cs
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Scanner111.UI.ViewModels;
using Scanner111.UI.Views;

namespace Scanner111.UI;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Type> _viewModelToViewMapping;

    public ViewLocator()
    {
        // Initialize view model to view mapping
        _viewModelToViewMapping = new Dictionary<Type, Type>
        {
            { typeof(DashboardViewModel), typeof(DashboardView) },
            { typeof(GameListViewModel), typeof(GameListView) },
            { typeof(GameDetailViewModel), typeof(GameDetailView) },
            { typeof(CrashLogListViewModel), typeof(CrashLogListView) },
            { typeof(CrashLogDetailViewModel), typeof(CrashLogDetailView) },
            { typeof(PluginAnalysisViewModel), typeof(PluginAnalysisView) },
            { typeof(SettingsViewModel), typeof(SettingsView) }
        };
    }

    public Control? Build(object? param)
    {
        if (param is null)
            return null;
            
        var viewModelType = param.GetType();
        
        // Try to find view type from mapping
        if (_viewModelToViewMapping.TryGetValue(viewModelType, out var viewType))
        {
            return (Control)Activator.CreateInstance(viewType)!;
        }
        
        // Fallback to conventional naming if not in mapping
        var name = viewModelType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = $"View not found: {name}" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}