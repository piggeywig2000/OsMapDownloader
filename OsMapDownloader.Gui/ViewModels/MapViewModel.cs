using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;
using OsMapDownloader.Gui.Views;

namespace OsMapDownloader.Gui.ViewModels
{
    internal class MapViewModel : INotifyPropertyChanged
    {
        private MapView Map { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _zoomInEnabled = true;
        public bool ZoomInEnabled
        {
            get => _zoomInEnabled;
            set
            {
                if (value != _zoomInEnabled)
                {
                    _zoomInEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _zoomOutEnabled = true;
        public bool ZoomOutEnabled
        {
            get => _zoomOutEnabled;
            set
            {
                if (value != _zoomOutEnabled)
                {
                    _zoomOutEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _scaleButtonContent = "Auto";
        public string ScaleButtonContent
        {
            get => _scaleButtonContent;
            set
            {
                if (value != _scaleButtonContent)
                {
                    _scaleButtonContent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _areaSelected = false;
        public bool AreaSelected
        {
            get => _areaSelected;
            set
            {
                if (value != _areaSelected)
                {
                    _areaSelected = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _snapToPoints = false;
        public bool SnapToPoints
        {
            get => _snapToPoints;
            set
            {
                if (value != _snapToPoints)
                {
                    _snapToPoints = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _snapToGrid = false;
        public bool SnapToGrid
        {
            get => _snapToGrid;
            set
            {
                if (value != _snapToGrid)
                {
                    _snapToGrid = value;
                    NotifyPropertyChanged();
                }
            }
        }

        //private StandardCursorType _cursorType = StandardCursorType.Arrow;
        //public StandardCursorType CursorType
        //{
        //    get => _cursorType;
        //    set
        //    {
        //        if (value != _cursorType)
        //        {
        //            _cursorType = value;
        //            NotifyPropertyChanged(nameof(ZoomBorderCursor));
        //        }
        //    }
        //}
        //private Cursor ZoomBorderCursor { get => new Cursor(CursorType); }

        private Cursor _zoomBorderCursor;
        public Cursor ZoomBorderCursor
        {
            get => _zoomBorderCursor;
            set
            {
                if (value != _zoomBorderCursor)
                {
                    _zoomBorderCursor = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public MapViewModel(MapView map, Cursor initialCursor)
        {
            Map = map;
            map.DataContext = this;
            _zoomBorderCursor = initialCursor;
        }

        public void ToggleScaleLock()
        {
            Scale? newScale = Map.ScaleLock switch
            {
                null => Scale.Explorer,
                Scale.Explorer => Scale.Landranger,
                Scale.Landranger => null,
                _ => throw new Exception("Scale Lock has an invalid value")
            };
            ScaleButtonContent = newScale switch
            {
                null => "Auto",
                Scale.Explorer => "1:25k",
                Scale.Landranger => "1:50k",
                _ => throw new Exception("Scale Lock has an invalid value")
            };
            Map.ChangeScaleLock(newScale);
        }

        public void ToggleSnapToPoints()
        {
            SnapToPoints = !SnapToPoints;
        }

        public void ToggleSnapToGrid()
        {
            SnapToGrid = !SnapToGrid;
        }
    }
}
