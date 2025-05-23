using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;

namespace Scanner111.ViewModels
{
    public class HelpDialogViewModel : ViewModelBase
    {
        private readonly Window _owner;
        private string _title;
        private string _content;

        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public ICommand CloseCommand { get; }

        public HelpDialogViewModel(Window owner, string title, string content)
        {
            _owner = owner;
            _title = title;
            _content = content;

            CloseCommand = ReactiveCommand.Create(() => _owner.Close());
        }
    }
}
