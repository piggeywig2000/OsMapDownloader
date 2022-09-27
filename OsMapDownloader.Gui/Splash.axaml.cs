using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui
{
    public partial class Splash : Window
    {
        public Splash()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
