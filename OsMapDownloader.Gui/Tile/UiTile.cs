using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OsMapDownloader.Coords;
using OsMapDownloader.WebDownloader;

namespace OsMapDownloader.Gui.Tile
{
    public class UiTile : IDisposable
    {
        private bool hasDisposed;
        private readonly TileManager parentManager;
        private Image? imageControl = null;
        private readonly WebTile webTile;
        private CancellationTokenSource? cancelSource;

        public IntCoordinate TopLeft => webTile.TopLeft;
        public Scale MapScale => webTile.MapScale;
        public byte Zoom => webTile.Zoom;

        public bool IsShowing { get; private set; } = false;
        public bool HasAttemptedDownload { get; private set; } = false;

        public event EventHandler<TileManager.TileImageEventArgs>? OnTileAdded;
        public event EventHandler<TileManager.TileImageEventArgs>? OnTileRemoved;

        public UiTile(TileManager parentManager, IntCoordinate topLeft, Scale mapScale, byte zoom)
        {
            this.parentManager = parentManager;
            webTile = new WebTile(topLeft, mapScale, zoom);
            Show();
        }

        public static Scale GetScaleFromZoom(byte zoom, Scale? scaleLock)
        {
            if (scaleLock != null && zoom >= 9) return scaleLock.Value;

            if (zoom >= 15) return Scale.Explorer;
            else if (zoom >= 13) return Scale.Landranger;
            else if (zoom >= 11) return Scale.Road;
            else return Scale.MiniScale;
        }

        public void Show()
        {
            if (IsShowing) return;
            IsShowing = true;

            if (!HasAttemptedDownload)
            {
                cancelSource = new CancellationTokenSource();
                _ = CreateImageAsync(cancelSource.Token); //Fire and forget download
            }
            
            if (imageControl != null)
            {
                imageControl.IsVisible = true;
            }
        }

        public void Hide()
        {
            if (!IsShowing) return;
            IsShowing = false;

            cancelSource?.Cancel();
            cancelSource = null;

            if (imageControl != null)
            {
                imageControl.IsVisible = false;
            }
        }

        private async Task CreateImageAsync(CancellationToken cancellationToken)
        {
            try
            {
                byte[]? data = await parentManager.DownloadTileDataAsync(webTile, cancellationToken);
                //If the tile does not exist, just don't create the image
                if (data == null)
                {
                    HasAttemptedDownload = true;
                    return;
                }

                //Create image control from Png image
                using MemoryStream ms = new MemoryStream(data, false);
                ms.Position = 0;
                Bitmap imageControlData = new Bitmap(ms);
                imageControl = new Image()
                {
                    Source = imageControlData,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Stretch = Avalonia.Media.Stretch.None,
                    IsVisible = true
                };
                Canvas.SetLeft(imageControl, TopLeft.X * 256);
                Canvas.SetTop(imageControl, TopLeft.Y * 256);
                OnTileAdded?.Invoke(this, new TileManager.TileImageEventArgs(imageControl));
                HasAttemptedDownload = true;
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!hasDisposed)
            {
                if (disposing)
                {
                    //Dispose managed objects in here
                }

                if (IsShowing)
                {
                    cancelSource?.Cancel();
                }
                if (imageControl != null)
                {
                    OnTileRemoved?.Invoke(this, new TileManager.TileImageEventArgs(imageControl));
                }
                //Set large fields to null here
                hasDisposed = true;
            }
        }

        ~UiTile()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
