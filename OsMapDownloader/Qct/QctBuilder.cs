using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Border;
using OsMapDownloader.Coords;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct.InterpolationMatrix;
using OsMapDownloader.Qct.WebDownloader;
using Serilog;
using SixLabors.ImageSharp;

namespace OsMapDownloader.Qct
{
    public static class QctBuilder
    {
        public static ProgressTracker CreateProgress() => new ProgressTracker(
                (new ProgressPhases("Getting set up", new ProgressPhase("Calculating map area", 1), new ProgressPhase("Creating tiles", 2)), 1),
                (new ProgressPhases("Calculating geographical referencing polynomial coefficients", new ProgressPhase("Generating sample coordinates", 0.75), new ProgressPhase("Converting sample coordinates", 20), new ProgressPhase("Calculating coefficient 1", 3), new ProgressPhase("Calculating coefficient 2", 3), new ProgressPhase("Calculating coefficient 3", 3), new ProgressPhase("Calculating coefficient 4", 3)), 15),
                (new ProgressCollection("Downloading images", "image"), 30),
                (new ProgressCollection("Processing tiles", "tile"), 80),
                (new ProgressPhases("Finishing up", new ProgressPhase("Writing metadata", 1), new ProgressPhase("Deleting images", 3)), 2));

        /// <summary>
        /// Generate a QCT map file from a map
        /// </summary>
        /// <param name="map">The map to generate from</param>
        /// <param name="filePath">The path where the file should be saved</param>
        /// <param name="shouldOverwrite">Whether, if the file already exists, it should be overwritten</param>
        /// <param name="polynomialSampleSize">The number of rows and columns in the grid of samples used for calculating the Geographical Referencing Polynomials</param>
        /// <param name="token">The token to use when downloading. Can be null to fetch one automatically</param>
        /// <param name="keepDownloadedTiles">Enable to keep instead of deleting the individual tile images</param>
        /// <param name="disableHardwareAccel">Enable to process tiles on the CPU instead of the GPU</param>
        public static async Task Build(Map map, ProgressTracker progress, QctMetadata metadata, string filePath, bool shouldOverwrite, int polynomialSampleSize, string? token, bool keepDownloadedTiles, bool disableHardwareAccel, CancellationToken cancellationToken = default(CancellationToken))
        {
            //The amount of tiles required to make a horizontal/vertical line in the bounding box
            //1:25000 uses 400px for 1k blue squares. So 1 tile = 64px = 160m
            //160 / 25000 = 0.0064
            double metersPerTileScaleMultiplier = 0.0064;
            double metersPerTile = (uint)map.Scale * metersPerTileScaleMultiplier; //160 at 1:25000
            double pixelsPerMeter = 64 / metersPerTile; //0.4 at 1:25000
            uint tilesWidth = (uint)Math.Ceiling((map.BottomRight.Easting - map.TopLeft.Easting) / metersPerTile);
            uint tilesHeight = (uint)Math.Ceiling((map.TopLeft.Northing - map.BottomRight.Northing) / metersPerTile);
            uint totalTiles = tilesWidth * tilesHeight;

            //Warn about potential overflow
            if (totalTiles * 2000 > uint.MaxValue) //Average tile size was found to be around 1751.557701 bytes. Assume 2000, and assume 4 billion to be the uint max
                Log.Warning("This map size could cause an error due to it being too large");

            //Fill the tiles array with the tile objects containing their position, and calculate map area
            Tile[] tiles = await PrepareObjects(map, progress.CurrentProgress!, tilesWidth, tilesHeight, metersPerTile, cancellationToken);

            //Calculate geographical referencing polynomials
            GeographicalReferencingCoefficients coefficients = await CalculateGeographicalReferencingPolynomials(map, progress.CurrentProgress!, polynomialSampleSize, pixelsPerMeter, cancellationToken);

            try
            {
                //Create working folder
                Directory.CreateDirectory("working");
            }
            catch (IOException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
            }

            try
            {
                //Download the images for the tiles
                Color[] palette = await DownloadRequiredImagesAndGetPalette(map, progress.CurrentProgress!, tiles, token, cancellationToken);
                byte[] interpolationMatrix = GenerateInterpolationMatrix(map, palette);

                //Write the QCT file while processing the tiles
                Log.Debug("Process Tiles");
                QctWriter builder = new QctWriter(progress, coefficients, palette, interpolationMatrix, metadata, tilesWidth, tilesHeight);
                await builder.Build(tiles, map.Area, filePath, shouldOverwrite, disableHardwareAccel, cancellationToken);
            }
            finally
            {
                try
                {
                    //Delete working folder
                    if (!keepDownloadedTiles)
                        await Task.Run(() => Directory.Delete("working", true));
                }
                catch (IOException e)
                {
                    throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                }
            }

            progress.CurrentProgress!.Report(2);
        }

        /// <summary>
        /// Fill the tiles array with tile objects containing the position of each tile
        /// </summary>
        private static async Task<Tile[]> PrepareObjects(Map map, IProgress<double> progress, uint tilesWidth, uint tilesHeight, double metersPerTile, CancellationToken cancellationToken)
        {
            Log.Debug("Calculating triangles for map area");
            try
            {
                await Task.Run(map.Area.CalculateVerticesAndTriangles);
            }
            catch (TriangleGenerationException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.BorderNonSimple, e);
            }
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(1);

            Log.Debug("Populate Tiles Array");
            uint totalTiles = tilesWidth * tilesHeight;
            Tile[] tiles = new Tile[totalTiles];

            Log.Debug("This map is {width} tiles wide by {height} tiles high. {total} tiles in total", tilesWidth, tilesHeight, totalTiles);

            await Task.Run(() =>
            {
                for (uint i = 0; i < tiles.Length; i++)
                {
                    Log.Verbose("Processing tile {index} / {total}", i + 1, totalTiles);

                    //Create new tile object at the correct top left corner location
                    tiles[i] = new Tile(new Osgb36Coordinate(
                        map.TopLeft.Easting + ((i % tilesWidth) * metersPerTile),
                        map.TopLeft.Northing - (Math.Floor((double)i / (double)tilesWidth) * metersPerTile)
                    ), map.Scale, metersPerTile);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            progress.Report(2);
            return tiles;
        }

        private static async Task<GeographicalReferencingCoefficients> CalculateGeographicalReferencingPolynomials(Map map, IProgress<double> progress, int polynomialSampleSize, double pixelsPerMeter, CancellationToken cancellationToken = default(CancellationToken))
        {
            GeographicalReferencingCoefficients coefficients;
            try
            {
                coefficients = await Task.Run(() => PolynomialCalculator.Calculate(progress, map.TopLeft, map.BottomRight, polynomialSampleSize, pixelsPerMeter, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OutOfMemoryException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.PolynomialCalculationOutOfMemory, e);
            }

            return coefficients;
        }

        /// <summary>
        /// Download the required images for each tile
        /// </summary>
        private static async Task<Color[]> DownloadRequiredImagesAndGetPalette(Map map, IProgress<double> progress, Tile[] tiles, string? token, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Debug("Download Required Images");

            TileDownloader downloader = new TileDownloader(progress, tiles, map.Area, map.Scale);

            try
            {
                await downloader.DownloadTilesAndGeneratePalette(token, cancellationToken);
            }
            catch (HttpRequestException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.DownloadError, e);
            }
            return downloader.Palette;
        }

        private static byte[] GenerateInterpolationMatrix(Map map, Color[] palette)
        {
            Log.Debug("Generating Interpolation Matrix");

            InterpolationMatrixCreator creator = new InterpolationMatrixCreator(palette, map.Scale);
            return creator.GetInterpolationMatrix();
        }
    }
}
