using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class ConfirmationDialog : Window
    {
        public string Prompt { get; }

        public ConfirmationDialog() : this("Example title", "Example prompt") { }
        public ConfirmationDialog(string title, string prompt)
        {
            DataContext = this;
            Title = title;
            Prompt = prompt;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void YesButtonPressed()
        {
            Close(true);
        }

        public void NoButtonPressed()
        {
            Close(false);
        }
    }
}
