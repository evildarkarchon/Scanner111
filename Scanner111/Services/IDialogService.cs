using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for displaying dialogs to the user
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Displays an information dialog with an OK button
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        Task ShowInfoDialogAsync(string title, string message);
        
        /// <summary>
        /// Displays an error dialog with an OK button
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The error message to display</param>
        Task ShowErrorDialogAsync(string title, string message);
        
        /// <summary>
        /// Displays a Yes/No question dialog and returns the user's choice
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The question to display</param>
        /// <param name="yesText">Text for the Yes button</param>
        /// <param name="noText">Text for the No button</param>
        /// <returns>True if Yes was selected, false otherwise</returns>
        Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText = "Yes", string noText = "No");
    }
}
