using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;

namespace Scanner111.ViewModels;

public class ConfirmationDialogViewModel : ViewModelBase
{
    private readonly Window _owner;
    private string _message;
    private string _noButtonText;
    private string _title;
    private string _yesButtonText;

    public ConfirmationDialogViewModel()
    {
        // Designer constructor
    }

    public ConfirmationDialogViewModel(Window owner, string title, string message, string yesText, string noText)
    {
        _owner = owner;
        _title = title;
        _message = message;
        _yesButtonText = yesText;
        _noButtonText = noText;
        Result = false;

        YesCommand = ReactiveCommand.Create(() =>
        {
            Result = true;
            _owner.Close(Result);
        });

        NoCommand = ReactiveCommand.Create(() =>
        {
            Result = false;
            _owner.Close(Result);
        });
    }

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public string YesButtonText
    {
        get => _yesButtonText;
        set => this.RaiseAndSetIfChanged(ref _yesButtonText, value);
    }

    public string NoButtonText
    {
        get => _noButtonText;
        set => this.RaiseAndSetIfChanged(ref _noButtonText, value);
    }

    public bool Result { get; private set; }

    public ICommand YesCommand { get; }
    public ICommand NoCommand { get; }
}