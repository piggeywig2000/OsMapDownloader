using System;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            Panel container = this.Find<Panel>("LicensesContainer");
            foreach (Control child in container.Children)
            {
                if (child is TextBlock textBlock && child.Classes.Contains("License"))
                {
                    using StringReader reader = new StringReader(textBlock.Text);
                    StringBuilder builder = new StringBuilder();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        builder.Append(line.Trim());
                        builder.Append(Environment.NewLine);
                    }
                    textBlock.Text = builder.ToString();
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
