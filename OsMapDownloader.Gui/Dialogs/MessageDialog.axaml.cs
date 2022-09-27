using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class MessageDialog : Window
    {
        public string Prompt { get; }

        public MessageDialog() : this("Example title", "Example prompt") { }
        public MessageDialog(string title, string prompt)
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

        public void OkButtonPressed()
        {
            Close();
        }
    }
}
