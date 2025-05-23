using System;
using Microsoft.Extensions.Logging;
using Scanner111.Services;
using ReactiveUI;

namespace Scanner111.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
}

