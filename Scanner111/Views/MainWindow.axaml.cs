﻿using Avalonia.Controls;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}