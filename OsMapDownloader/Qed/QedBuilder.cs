using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Coords;
using OsMapDownloader.Progress;
using Serilog;

namespace OsMapDownloader.Qed
{
    public static class QedBuilder
    {
        public static ProgressTracker CreateProgress() => new ProgressTracker(
            (new ProgressPhases("Calculating geographical referencing polynomial coefficients", new ProgressPhase("Generating sample coordinates", 0.75), new ProgressPhase("Converting sample coordinates", 20), new ProgressPhase("Calculating coefficient 1", 3), new ProgressPhase("Calculating coefficient 2", 3), new ProgressPhase("Calculating coefficient 3", 3), new ProgressPhase("Calculating coefficient 4", 3)), 20),
            (new ProgressCollection("Downloading elevation data file", "MB"), 40),
            (new ProgressCollection("Writing tiles", "tiles"), 40));

        public static async Task Build(Map map, ProgressTracker progress, string filePath, bool shouldOverwrite, int polynomialSampleSize, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Information("Exporting QED file");
            progress.CurrentProgress!.Report(0);

            filePath = Path.ChangeExtension(filePath, "qed");

            //Every map has 1km x 1km tiles
            //Each tile contains 20 x 20 data points, so data points are 50m apart
            const uint pointsPerTile = 20;
            const uint metersPerTile = 1000;
            double pointsPerMeter = pointsPerTile / (double)metersPerTile;
            double metersBetweenPoints = metersPerTile / (double)pointsPerTile;
            Osgb36Coordinate tl = new Osgb36Coordinate(Math.Floor(map.TopLeft.Easting / metersBetweenPoints) * metersBetweenPoints, Math.Ceiling(map.TopLeft.Northing / metersBetweenPoints) * metersBetweenPoints);
            Osgb36Coordinate br = new Osgb36Coordinate(Math.Ceiling(map.BottomRight.Easting / metersBetweenPoints) * metersBetweenPoints, Math.Floor(map.BottomRight.Northing / metersBetweenPoints) * metersBetweenPoints);
            uint tilesWidth = (uint)Math.Ceiling((br.Easting - tl.Easting) / metersPerTile);
            uint tilesHeight = (uint)Math.Ceiling((tl.Northing - br.Northing) / metersPerTile);
            uint totalTiles = tilesWidth * tilesHeight;

            //Create tiles
            Log.Debug("Populate Tiles Array");
            Tile[] tiles = new Tile[totalTiles];
            Log.Debug("This map is {width} tiles wide by {height} tiles high. {total} tiles in total", tilesWidth, tilesHeight, totalTiles);
            for (int i = 0; i < totalTiles; i++)
            {
                tiles[i] = new Tile(new Osgb36Coordinate(
                    tl.Easting + (i % tilesWidth) * metersPerTile,
                    tl.Northing - Math.Floor((double)i / (double)tilesWidth) * metersPerTile));
            }

            //Calculate polynomial coefficients
            GeographicalReferencingCoefficients coefficients = await CalculateGeographicalReferencingPolynomials(tl, br, progress.CurrentProgress!, polynomialSampleSize, pointsPerMeter, cancellationToken);

            //Download terrain data
            await DownloadElevationData(progress.CurrentProgress!);

            //Export QED file
            Log.Debug("Write QED file");
            QedWriter writer = new QedWriter(progress, coefficients, tilesWidth, tilesHeight, pointsPerTile, metersPerTile, metersBetweenPoints);
            await writer.Write(tiles, map.Area, filePath, shouldOverwrite, cancellationToken);
        }

        private static async Task<GeographicalReferencingCoefficients> CalculateGeographicalReferencingPolynomials(Osgb36Coordinate topLeft, Osgb36Coordinate bottomRight, IProgress<double> progress, int polynomialSampleSize, double pixelsPerMeter, CancellationToken cancellationToken = default(CancellationToken))
        {
            GeographicalReferencingCoefficients coefficients;
            try
            {
                coefficients = await Task.Run(() => PolynomialCalculator.Calculate(progress, topLeft, bottomRight, polynomialSampleSize, pixelsPerMeter, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OutOfMemoryException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.PolynomialCalculationOutOfMemory, e);
            }

            return coefficients;
        }

        private static async Task DownloadElevationData(IProgress<double> progress, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Debug("Download Elevation Data");

            double length;
            if (File.Exists("terrain50.zip"))
            {
                Log.Debug("Elevation data file already exists, skipping download");
                length = new FileInfo("terrain50.zip").Length / 1000000;
                ((ProgressCollection)progress).TotalItems = length;
                progress.Report(length);
                return;
            }

            //We need to download it if we got here
            using HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.os.uk/downloads/v1/products/Terrain50/downloads?area=GB&format=ASCII+Grid+and+GML+%28Grid%29&redirect"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            length = (double)response.Content.Headers.ContentLength! / 1000000.0;
            ((ProgressCollection)progress).TotalItems = length;

            using FileStream fs = File.Create("terrain50.zip");
            Task dlTask = response.Content.CopyToAsync(fs, cancellationToken);
            while (!dlTask.IsCompleted)
            {
                await Task.Delay(1000);
                double current = fs.Position / 1000000.0;
                if (current != length) progress.Report(current);
                cancellationToken.ThrowIfCancellationRequested();
            }
            progress.Report(length);
        }
    }
}
