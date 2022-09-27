using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsMapDownloader.Border;
using OsMapDownloader.Progress;
using SixLabors.ImageSharp;

namespace OsMapDownloader.WebDownloader
{
    public class TileDownloader
    {
        private readonly ILogger log;
        private readonly IProgress<double> progress;
        private readonly Tile[] _tiles;
        private readonly MapArea _area;
        private readonly Scale _mapScale;
        private readonly PaletteCreator _paletteCreator;

        public Color[] Palette { get => _paletteCreator.GetPalette().Select(rgb => Color.FromPixel(rgb)).ToArray(); }

        public TileDownloader(ILogger logger, IProgress<double> progress, Tile[] tiles, MapArea area, Scale mapScale)
        {
            log = logger;
            this.progress = progress;
            _tiles = tiles;
            _area = area;
            _mapScale = mapScale;
            _paletteCreator = new PaletteCreator(mapScale);
        }

        private struct WebTileCoord
        {
            public WebTileCoord(int x, int y)
            {
                X = x;
                Y = y;
            }
            public int X;
            public int Y;
        }

        public async Task DownloadTilesAndGeneratePalette(string? token, CancellationToken cancellationToken = default(CancellationToken))
        {
            HashSet<WebTileCoord> tilesToDownload = new HashSet<WebTileCoord>();
            foreach (Tile tile in _tiles)
            {
                tile.GetRequiredWebTileRange();
                //Don't download if tile falls outside of area
                if (_area.IsRectangleInArea(tile.TopLeft.Easting, tile.TopLeft.Northing, tile.BottomRight.Easting, tile.BottomRight.Northing))
                {
                    int currentX = tile.WebTileMinX;
                    int currentY = tile.WebTileMinY;
                    while (currentY <= tile.WebTileMaxY)
                    {
                        tilesToDownload.Add(new WebTileCoord(currentX, currentY));

                        currentX++;
                        if (currentX > tile.WebTileMaxX)
                        {
                            currentX = tile.WebTileMinX;
                            currentY++;
                        }
                    }
                }
            }

            ((ProgressCollection)progress).TotalItems = (uint)tilesToDownload.Count;

            using HttpClient client = new HttpClient();

            if (string.IsNullOrEmpty(token))
            {
                log.LogDebug("Fetching token");
                token = await WebTile.GetToken(client, cancellationToken);
                log.LogDebug("Fetched token: {token}", token);
            }

            const int TASK_QUEUE_SIZE = 10;
            List<Task<byte[]?>> runningTasks = new List<Task<byte[]?>>(TASK_QUEUE_SIZE);
            int numTotal = tilesToDownload.Count;
            int numCompleted = 0;
            IEnumerator<WebTileCoord> coordEnumerator = tilesToDownload.GetEnumerator();
            DateTime lastReportTime = DateTime.UtcNow;
            while (numCompleted < numTotal)
            {
                if (runningTasks.Count < TASK_QUEUE_SIZE && numCompleted + runningTasks.Count < numTotal)
                {
                    //Task queue has space, spin up a new task
                    coordEnumerator.MoveNext();
                    WebTileCoord coord = coordEnumerator.Current;
                    WebTile tile = new WebTile(coord.X, coord.Y, _mapScale);
                    runningTasks.Add(DownloadTile(tile, client, token, cancellationToken));
                }

                if (runningTasks.Count == TASK_QUEUE_SIZE || numCompleted + runningTasks.Count >= numTotal)
                {
                    //Task queue is full or we have no more work waiting, wait for one to complete
                    Task<byte[]?> completedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(completedTask);
                    byte[]? data = completedTask.Result;
                    numCompleted++;

                    //Analyze tile colours to help make a Palette
                    if (data != null) 
                        _paletteCreator.AddImage(data);

                    log.LogDebug("Downloaded image {completed} / {total}", numCompleted, numTotal);
                    if (DateTime.UtcNow - lastReportTime > TimeSpan.FromSeconds(1))
                    {
                        lastReportTime = DateTime.UtcNow;
                        progress.Report(numCompleted);
                    }
                }
            }
            progress.Report(numTotal);
        }

        private async Task<byte[]?> DownloadTile(WebTile tile, HttpClient client, string token, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[]? data;

            try
            {
                //Only download if we don't already have the image
                if (!File.Exists("working/" + tile.FileName + ".png"))
                {
                    log.LogDebug("Downloading image with filename {fileName}", tile.FileName);
                    data = await tile.DownloadAsync(client, token, cancellationToken);

                    if (data != null)
                        await File.WriteAllBytesAsync($"working/{tile.FileName}.png", data, cancellationToken);
                }
                else
                {
                    data = await File.ReadAllBytesAsync($"working/{tile.FileName}.png", cancellationToken);
                }
            }
            catch (Exception e)
            {
                if (e is IOException || e is System.Security.SecurityException)
                {
                    throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                }
                throw;
            }

            return data;
        }
    }
}
