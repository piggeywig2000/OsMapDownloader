using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OsMapDownloader.Gui.Areas;
using OsMapDownloader.Gui.Config;
using OsMapDownloader.Gui.VIewModels;
using OsMapDownloader.Gui.Views;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct;
using Serilog;

namespace OsMapDownloader.Gui.Dialogs
{
    public partial class ExportDialog : Window, INotifyPropertyChanged
    {
        private readonly Area areaToExport;
        private readonly MetadataViewModel metadata;
        private readonly CancellationTokenSource exportCancelSource = new CancellationTokenSource();
        private readonly Settings settings;

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsExporting { get; private set; } = false;
        public bool ExportCompleted { get; private set; } = false;

        private int _page = 0;
        public int Page
        {
            get => _page;
            set
            {
                if (value != _page)
                {
                    _page = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(NextButtonContent));
                }
            }
        }

        public string NextButtonContent
        {
            get => Page == 0 ? "Next" : "Export";
        }

        public string CancelButtonContent
        {
            get => ExportCompleted ? "Close" : "Cancel";
        }

        private string _fileLocation = "";
        public string FileLocation
        {
            get => _fileLocation;
            set
            {
                if (value != _fileLocation)
                {
                    _fileLocation = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(IsFileLocationValid));
                }
            }
        }

        public bool IsFileLocationValid
        {
            get => !string.IsNullOrWhiteSpace(FileLocation);
        }

        private int _scaleIndex = 0;
        public int ScaleIndex
        {
            get => _scaleIndex;
            set
            {
                if (value != _scaleIndex)
                {
                    _scaleIndex = value;
                    Scale = _scaleIndex switch
                    {
                        0 => Scale.Explorer,
                        1 => Scale.Landranger,
                        2 => Scale.Road,
                        3 => Scale.MiniScale,
                        _ => throw new IndexOutOfRangeException("Scale index set to an invalid index")
                    };
                    NotifyPropertyChanged();
                }
            }
        }

        private Scale _scale = Scale.Explorer;
        public Scale Scale
        {
            get => _scale;
            set
            {
                if (value != _scale)
                {
                    _scale = value;
                    metadata.Scale = $"1:{(int)_scale:N0}";
                    metadata.Type = (int)_scale <= 50000 ? "Land" : "Road";
                }
            }
        }

        private double _overallProgress = 0;
        public double OverallProgress
        {
            get => _overallProgress;
            set
            {
                if (value != _overallProgress)
                {
                    _overallProgress = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private double _stageProgress = 0;
        public double StageProgress
        {
            get => _stageProgress;
            set
            {
                if (value != _stageProgress)
                {
                    _stageProgress = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _progessStageName = "";
        public string ProgressStageName
        {
            get => _progessStageName;
            set
            {
                if (value != _progessStageName)
                {
                    _progessStageName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _progessStageStatus = "";
        public string ProgressStageStatus
        {
            get => _progessStageStatus;
            set
            {
                if (value != _progessStageStatus)
                {
                    _progessStageStatus = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public ExportDialog() : this(new Area("Example area")) { }
        public ExportDialog(Area areaToExport)
        {
            settings = ((App)Application.Current!).ConfigInstance.Config;
            DataContext = this;
            this.areaToExport = areaToExport;
            metadata = new MetadataViewModel()
            {
                Name = areaToExport.Name,
                LongTitle = areaToExport.Name,
                Scale = "1:25,000",
                Type = "Land"
            };

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.Find<Metadata>("MetadataView").DataContext = metadata;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async void BrowseButtonPressed()
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                DefaultExtension = ".qct",
                InitialFileName = areaToExport.Name,
                Title = "Browse for QCT save location",
                Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "Quick Chart file", Extensions = new List<string>() { "qct" } } }
            };
            string? filePath = await dialog.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(filePath)) return;
            FileLocation = filePath;
        }

        public void BackButtonPressed()
        {
            Page--;
        }

        public async void NextButtonPressed()
        {
            Page++;
            if (Page == 2)
            {
                await StartExport();
            }
        }

        public void CancelButtonPressed()
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (IsExporting)
            {
                exportCancelSource.Cancel();
            }
            base.OnClosing(e);
        }

        private async Task StartExport()
        {
            IsExporting = true;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .CreateLogger();
            try
            {
                Map map = new Map(areaToExport.Points.ToArray(), Scale, metadata.UnderlyingMetadata);
                ProgressTracker progress = QctBuilder.CreateProgress();
                progress.ProgressChanged += (s, e) => UpdateProgress(progress);
                UpdateProgress(progress);
                await QctBuilder.Build(map, progress, FileLocation, true, settings.PolynomialSampleSize, settings.Token, settings.KeepTiles, !settings.UseHardwareAcceleration, exportCancelSource.Token);
            }
            catch (Exception e)
            {
                IsExporting = false;
                if (e is TaskCanceledException || e is OperationCanceledException) return;
                string message;
                if (e is MapGenerationException mapGenerationException)
                {
                    message = mapGenerationException.Reason switch
                    {
                        MapGenerationExceptionReason.BorderOutOfBounds => "A point in the map border is too far away from the UK. Invalid points are marked as red on the map.\n\nCheck that there are no red points on the map, and try again.",
                        MapGenerationExceptionReason.BorderNonSimple => "The map border is an invalid shape.\n\nCheck that:\n• The border does not cross over itself\n• There aren't two points in the same location\n• There aren't 3 points connected to each other in a perfectly straight line",
                        MapGenerationExceptionReason.PolynomialCalculationOutOfMemory => "The system ran out of memory while calculating the geographical referencing polynomial coefficients.\n\nTry reducing the polynomial sample size in settings and try again.",
                        MapGenerationExceptionReason.DownloadError => "An error occurred while downloading the images. Ordinance Survey have probably changed something on their website that broke this program.\n\nIt's possible that this could be fixed by providing your own download token in the settings.",
                        MapGenerationExceptionReason.OpenGLError => "An OpenGL error occurred while processing the tiles.\n\nCheck that your video drivers are up to date. If they are up to date and this error still occurs, try disabling hardware acceleration in settings.",
                        MapGenerationExceptionReason.IOError => "The file could not be written to.\n\nCheck that the save location provided is a valid folder. If the file is being overwritten, make sure that the file is not open in another program.",
                        _ => throw new ArgumentException("Invalid value for map generation exception reason")
                    };
                }
                else
                {
                    message = "An unknown error occurred while exporting. Error details are provided below:\n\n" + e.ToString();
                }
                await new MessageDialog("Export Error", message).ShowDialog(this);
                Close();
            }
        }

        private void UpdateProgress(ProgressTracker progress)
        {
            OverallProgress = progress.OverallProgress;
            StageProgress = progress.IsCompleted ? 1 : progress.CurrentProgressItem!.Value;
            ProgressStageName = progress.IsCompleted ? "Completed" : progress.CurrentProgressItem!.Name;
            ProgressStageStatus = progress.IsCompleted ? "" : progress.CurrentProgressItem!.Status;

            if (progress.IsCompleted)
            {
                IsExporting = false;
                ExportCompleted = true;
                NotifyPropertyChanged(nameof(CancelButtonContent));
            }
        }
    }
}
