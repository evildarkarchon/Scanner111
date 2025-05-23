using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia;
using Avalonia.Media;
using System;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;

namespace Scanner111.Services
{
    /// <summary>
    /// Implementation of the IDialogService interface using Avalonia dialogs
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Displays an information dialog with an OK button
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        public async Task ShowInfoDialogAsync(string title, string message)
        {
            await ShowBasicDialogAsync(title, message, "OK");
        }
        
        /// <summary>
        /// Displays an error dialog with an OK button
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The error message to display</param>
        public async Task ShowErrorDialogAsync(string title, string message)
        {
            await ShowBasicDialogAsync(title, message, "OK");
        }
        
        /// <summary>
        /// Displays a Yes/No question dialog and returns the user's choice
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The question to display</param>
        /// <param name="yesText">Text for the Yes button</param>
        /// <param name="noText">Text for the No button</param>
        /// <returns>True if Yes was selected, false otherwise</returns>
        public async Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText = "Yes", string noText = "No")
        {
            var result = false;
            var tcs = new TaskCompletionSource<bool>();
            
            var window = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                new Button
                                {
                                    Content = noText
                                },
                                new Button
                                {
                                    Content = yesText
                                }
                            }
                        }
                    }
                }
            };

            // Set up button click events properly
            var buttons = ((StackPanel)((StackPanel)window.Content).Children[1]).Children;
            var noButton = (Button)buttons[0];
            var yesButton = (Button)buttons[1];
            
            noButton.Click += (s, e) =>
            {
                tcs.SetResult(false);
                window.Close();
            };
            
            yesButton.Click += (s, e) =>
            {
                tcs.SetResult(true);
                window.Close();
            };

            window.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(false);
            };

            // Get the current active window to use as parent
            Window? parentWindow = null;
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                parentWindow = desktop.MainWindow;
            }
            
            if (parentWindow != null)
                await window.ShowDialog(parentWindow);
            else
                window.Show(); // Fallback to non-modal window if no parent is found
                
            result = await tcs.Task;
            
            return result;
        }
        
        private async Task ShowBasicDialogAsync(string title, string message, string buttonText)
        {
            var tcs = new TaskCompletionSource();
            
            var window = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new Button
                        {
                            Content = buttonText,
                            HorizontalAlignment = HorizontalAlignment.Right
                        }
                    }
                }
            };
            
            // Set up button click event properly
            var button = (Button)((StackPanel)window.Content).Children[1];
            button.Click += (s, e) =>
            {
                tcs.SetResult();
                window.Close();
            };
            
            window.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult();
            };

            // Get the current active window to use as parent
            Window? parentWindow = null;
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                parentWindow = desktop.MainWindow;
            }
            
            if (parentWindow != null)
                await window.ShowDialog(parentWindow);
            else
                window.Show(); // Fallback to non-modal window if no parent is found
                
            await tcs.Task;
        }
    }
}
