using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class TextDialog : Window, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _response = "";
        public string Response
        {
            get => _response;
            set
            {
                if (value != _response)
                {
                    _response = value.Trim();
                    NotifyPropertyChanged();
                    OkButtonEnabled = !string.IsNullOrWhiteSpace(_response);
                }
            }
        }

        public string Prompt { get; }
        
        private bool _okButtonEnabled = false;
        public bool OkButtonEnabled
        {
            get => _okButtonEnabled;
            set
            {
                if (value != _okButtonEnabled)
                {
                    _okButtonEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TextDialog() : this("Example title", "Example prompt") { }
        public TextDialog(string title, string prompt, string? initialValue = null)
        {
            DataContext = this;
            Title = title;
            Prompt = prompt;
            if (!string.IsNullOrWhiteSpace(initialValue))
                Response = initialValue;

            Opened += (s, e) => this.Find<TextBox>("InputTextBox").Focus();

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void OkButtonPressed()
        {
            Close(Response);
        }

        public void CancelButtonPressed()
        {
            Close(null);
        }
    }
}
