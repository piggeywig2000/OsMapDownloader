using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OsMapDownloader.Gui.Config;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class SettingsDialog : Window, INotifyPropertyChanged
    {
        private readonly ConfigLoader<Settings> configLoader;
        private Settings settings => configLoader.Config;

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int PolynomialSampleSize
        {
            get => settings.PolynomialSampleSize;
            set
            {
                settings.PolynomialSampleSize = value;
                _ = configLoader.Save();
            }
        }

        public string? Token
        {
            get => settings.Token;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                settings.Token = value;
                _ = configLoader.Save();
            }
        }

        public bool UseHardwareAcceleration
        {
            get => settings.UseHardwareAcceleration;
            set
            {
                settings.UseHardwareAcceleration = value;
                _ = configLoader.Save();
            }
        }

        public bool KeepTiles
        {
            get => settings.KeepTiles;
            set
            {
                settings.KeepTiles = value;
                _ = configLoader.Save();
            }
        }

        private bool _isDetailsShowing = false;
        public bool IsDetailsShowing
        {
            get => _isDetailsShowing;
            set
            {
                if (value != _isDetailsShowing)
                {
                    _isDetailsShowing = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _detailsTitle = "";
        public string DetailsTitle
        {
            get => _detailsTitle;
            set
            {
                if (value != _detailsTitle)
                {
                    _detailsTitle = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _detailsDescription = "";
        public string DetailsDescription
        {
            get => _detailsDescription;
            set
            {
                if (value != _detailsDescription)
                {
                    _detailsDescription = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public SettingsDialog()
        {
            configLoader = ((App)Application.Current!).ConfigInstance;
            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            InputElement element;
            element = this.Find<InputElement>("PolynomialSampleSizeElement");
            element.PointerEnter += (s, e) => ShowDetails("Polynomial Coefficients Sample Size", "The number of rows and columns in the grid of samples taken when calculating the polynomial coefficients for GPS coordinate transformations.\n\nIncrease this for a higher GPS accuracy, decrease this for lower memory usage and a faster processing time.\n\nIf unsure, set this to 2500.");
            element.PointerLeave += (s, e) => HideDetails();
            element = this.Find<InputElement>("TokenElement");
            element.PointerEnter += (s, e) => ShowDetails("Download Token", "The token to use when downloading tiles.\n\nBy default the program will try to fetch this automatically, but you can manually specify it with this option if that doesn't work.\n\nIf unsure, leave this box blank.");
            element.PointerLeave += (s, e) => HideDetails();
            element = this.Find<InputElement>("UseHardwareAccelerationElement");
            element.PointerEnter += (s, e) => ShowDetails("Use Hardware Acceleration", "Whether or not to use the GPU when processing the tiles.\n\nThis will significantly speed up processing speed, so only disable this if you're having issues.\n\nIf unsure, leave this checked.");
            element.PointerLeave += (s, e) => HideDetails();
            element = this.Find<InputElement>("KeepTilesElement");
            element.PointerEnter += (s, e) => ShowDetails("Keep Downloaded Tiles", "Whether or not to delete the individual downloaded tile images stored in the \"working\" folder after exporting.\n\nIf you're exporting the same area at same scale multiple times, this will prevent having to redownload the tiles that were deleted after the previous export.\n\nIf unsure, leave this unchecked.");
            element.PointerLeave += (s, e) => HideDetails();

            element = this.Find<InputElement>("DetailsContainer");
            element.PointerEnter += (s, e) => ShowDetails(DetailsTitle, DetailsDescription);
            element.PointerLeave += (s, e) => HideDetails();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ShowDetails(string title, string description)
        {
            DetailsTitle = title;
            DetailsDescription = description;
            IsDetailsShowing = true;
        }

        private void HideDetails()
        {
            IsDetailsShowing = false;
        }

        private async void ResetToDefault()
        {
            Settings defaults = new Settings();
            settings.PolynomialSampleSize = defaults.PolynomialSampleSize;
            settings.Token = defaults.Token;
            settings.UseHardwareAcceleration = defaults.UseHardwareAcceleration;
            settings.KeepTiles = defaults.KeepTiles;
            NotifyPropertyChanged(nameof(PolynomialSampleSize));
            NotifyPropertyChanged(nameof(Token));
            NotifyPropertyChanged(nameof(UseHardwareAcceleration));
            NotifyPropertyChanged(nameof(KeepTiles));
            await configLoader.Save();
        }
    }
}
