using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using OsMapDownloader.Coords;
using OsMapDownloader.Qct.WebDownloader;

namespace OsMapDownloader.Gui.Tile
{
    public class TileManager
    {
        private const int MAX_CAPACITY = 1000;
        private const int MAX_CONCURRENT_DOWNLOADS = 16;

        private readonly HttpClient httpClient;
        private readonly List<UiTile> tiles = new List<UiTile>();
        private readonly SemaphoreSlim downloadSemaphore = new SemaphoreSlim(MAX_CONCURRENT_DOWNLOADS);
        private readonly SemaphoreSlim tokenSemaphore = new SemaphoreSlim(1);

        private bool allowTokenRefetch = true;
        private string? _webToken = null;
        public string? WebToken
        {
            get => _webToken;
            set
            {
                if (_webToken != value)
                {
                    _webToken = value;
                    allowTokenRefetch = true;
                }
            }
        }

        public class TileImageEventArgs : EventArgs
        {
            public Image ImageControl { get; }

            public TileImageEventArgs(Image image) : base()
            {
                ImageControl = image;
            }
        }
        public event EventHandler<TileImageEventArgs>? OnTileAdded;
        public event EventHandler<TileImageEventArgs>? OnTileRemoved;

        public TileManager(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public void UpdateVisibleTiles(IntCoordinate topLeft, IntCoordinate bottomRight, Scale scale, byte zoom)
        {
            //Add new tiles
            for (int y = topLeft.Y; y <= bottomRight.Y; y++)
            {
                for (int x = topLeft.X; x <= bottomRight.X; x++)
                {
                    UiTile? existingTile = tiles.Find(tile => tile.TopLeft.X == x && tile.TopLeft.Y == y && tile.MapScale == scale && tile.Zoom == zoom);
                    if (existingTile != null)
                    {
                        tiles.Remove(existingTile);
                        if (!existingTile.IsShowing)
                            existingTile.Show();
                        tiles.Add(existingTile);
                    }
                    else
                    {
                        UiTile newTile = new UiTile(this, new IntCoordinate(x, y), scale, zoom);
                        newTile.OnTileAdded += (object? sender, TileImageEventArgs e) => OnTileAdded?.Invoke(sender, e);
                        newTile.OnTileRemoved += (object? sender, TileImageEventArgs e) => OnTileRemoved?.Invoke(sender, e);
                        tiles.Add(newTile);
                    }
                }
            }

            //Hide/remove tiles
            int i = 0;
            while (i < tiles.Count)
            {
                UiTile tile = tiles[i];
                if (tile.TopLeft.X < topLeft.X || tile.TopLeft.Y < topLeft.Y || tile.TopLeft.X > bottomRight.X || tile.TopLeft.Y > bottomRight.Y || tile.MapScale != scale || tile.Zoom != zoom)
                {
                    //We can hide/remove this
                    if (tiles.Count > MAX_CAPACITY)
                    {
                        tile.Dispose();
                        tiles.Remove(tile);
                        continue;
                    }
                    else tile.Hide();
                }
                i++;
            }
        }

        public async Task<byte[]?> DownloadTileDataAsync(WebTile webTile, CancellationToken cancellationToken)
        {
            byte[]? data = null;
            await downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (WebToken == null)
                {
                    await RefetchToken();
                }
                data = await webTile.DownloadAsync(httpClient, WebToken!, cancellationToken);
            }
            finally
            {
                downloadSemaphore.Release();
            }
            return data;
        }

        private async Task RefetchToken()
        {
            await tokenSemaphore.WaitAsync();
            if (!allowTokenRefetch)
            {
                tokenSemaphore.Release();
                return;
            }
            try
            {
                System.Diagnostics.Debug.WriteLine("Refetching token");
                WebToken = await WebTile.GetToken(httpClient);
            }
            catch (HttpRequestException)
            {
                //Ignore, we'll just leave the token alone
            }
            allowTokenRefetch = false;
            tokenSemaphore.Release();
        }
    }
}
