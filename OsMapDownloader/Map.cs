using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsMapDownloader.Coords;
using OsMapDownloader.Border;
using OsMapDownloader.InterpolationMatrix;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct;
using OsMapDownloader.WebDownloader;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OsMapDownloader
{
    public class Map
    {
        private readonly ILogger log;

        public ProgressTracker Progress { get; }

        public Osgb36Coordinate TopLeft { get; }
        public Osgb36Coordinate BottomRight { get; }
        public Osgb36Coordinate[] Border { get; }
        public MapArea Area { get; }
        public QctMetadata Metadata { get; }

        //The amount of tiles required to make a horizontal/vertical line in the bounding box
        //1:25000 uses 400px for 1k blue squares. So 1 tile = 64px = 160m
        //160 / 25000 = 0.0064
        public const double MetersPerTileScaleMultiplier = 0.0064;
        public Scale Scale { get; }
        public uint ScaleNum { get => (uint)Scale; }
        public double MetersPerTile { get => ScaleNum * MetersPerTileScaleMultiplier; } //160 at 1:25000
        public double PixelsPerMeter { get => 64 / MetersPerTile; } //0.4 at 1:25000
        public uint TilesWidth { get => (uint)Math.Ceiling((BottomRight.Easting - TopLeft.Easting) / MetersPerTile); }
        public uint TilesHeight { get => (uint)Math.Ceiling((TopLeft.Northing - BottomRight.Northing) / MetersPerTile); }
        public uint TotalTiles { get => TilesWidth * TilesHeight; }
        public uint PixelsWidth { get => TilesWidth * 64; }
        public uint PixelsHeight { get => TilesHeight * 64; }
        public uint TotalPixels { get => PixelsWidth * PixelsHeight; }

        private Tile[] Tiles;

        private QctGeographicalReferencingCoefficients _geographicalReferencingCoefficients = new QctGeographicalReferencingCoefficients();

        Color[] _palette = new Color[128];

        byte[] _interpolationMatrix = new byte[16384];

        /// <summary>
        /// Create a new map with a defined area
        /// </summary>
        /// <param name="topLeft">The top left corner of the bounding box</param>
        /// <param name="bottomRight">The bottom right corner of the bounding box</param>
        public Map(ILogger logger, Wgs84Coordinate[] borderPoints, Scale scale, QctMetadata metadata)
        {
            log = logger;
            Progress = new ProgressTracker(
                (new ProgressPhases("Getting set up", new ProgressPhase("Calculating map area", 1), new ProgressPhase("Creating tiles", 2)), 1),
                (new ProgressPhases("Calculating geographical referencing polynomial coefficients", new ProgressPhase("Generating sample coordinates", 0.75), new ProgressPhase("Converting sample coordinates", 20), new ProgressPhase("Calculating coefficient 1", 3), new ProgressPhase("Calculating coefficient 2", 3), new ProgressPhase("Calculating coefficient 3", 3), new ProgressPhase("Calculating coefficient 4", 3)), 15),
                (new ProgressCollection("Downloading images", "image"), 30),
                (new ProgressCollection("Processing tiles", "tile"), 80),
                (new ProgressPhases("Finishing up", new ProgressPhase("Writing metadata", 1), new ProgressPhase("Deleting images", 3)), 2));

            Scale = scale;
            Metadata = metadata;
            Metadata.MapOutline = borderPoints;

            if (borderPoints.Length < 3)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.BorderNonSimple);
            }

            try
            {
                Border = borderPoints.Select(point => point.ToOsgb36Accurate()).ToArray();
            }
            catch (KeyNotFoundException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.BorderOutOfBounds, e);
            }

            //Find corners
            TopLeft = new Osgb36Coordinate(Border.Min(point => point.Easting), Border.Max(point => point.Northing));
            BottomRight = new Osgb36Coordinate(Border.Max(point => point.Easting), Border.Min(point => point.Northing));

            //Validate bounds
            if (Math.Round(TopLeft.Easting) < 0 || Math.Round(TopLeft.Northing) > 1249000 || Math.Round(BottomRight.Easting) > 876248000 || Math.Round(BottomRight.Northing) < 0)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.BorderOutOfBounds);
            }

            log.LogDebug("This map has top left corner at {topLeft} and bottom right corner at {bottomRight} when converted to Eastings/Northings", TopLeft, BottomRight);
            log.LogDebug("This map has a scale of 1:{scale}", ScaleNum);

            Area = new MapArea(Border);
            Tiles = new Tile[TotalTiles];

            //Warn about potential overflow
            if (TotalTiles * 2000 > uint.MaxValue) //Average tile size was found to be around 1751.557701 bytes. Assume 2000, and assume 4 billion to be the uint max
                log.LogWarning("This map size could cause an error due to it being too large");
        }

        /// <summary>
        /// Generate a QCT map file from this map object
        /// </summary>
        /// <param name="filePath">The path where the file should be saved</param>
        /// <param name="shouldOverwrite">Whether, if the file already exists, it should be overwritten</param>
        /// <param name="polynomialSampleSize">The number of rows and columns in the grid of samples used for calculating the Geographical Referencing Polynomials</param>
        /// <param name="token">The token to use when downloading. Can be null to fetch one automatically</param>
        /// <param name="keepDownloadedTiles">Enable to keep instead of deleting the individual tile images</param>
        /// <param name="disableHardwareAccel">Enable to process tiles on the CPU instead of the GPU</param>
        public async Task GenerateQCTFileFromMap(string filePath, bool shouldOverwrite, int polynomialSampleSize, string? token, bool keepDownloadedTiles, bool disableHardwareAccel, CancellationToken cancellationToken = default(CancellationToken))
        {
            //Fill the tiles array with the tile objects containing their position, and calculate map area
            await PrepareObjects(cancellationToken);

            //Calculate geographical referencing polynomials
            await CalculateGeographicalReferencingPolynomials(polynomialSampleSize, cancellationToken);

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
                await DownloadRequiredImages(token, cancellationToken);

                //Process the tiles
                await ProcessTiles(filePath, shouldOverwrite, disableHardwareAccel, cancellationToken);
            }
            finally
            {
                try
                {
                    //Delete working folder
                    if (!keepDownloadedTiles)
                        Directory.Delete("working", true);
                }
                catch (IOException e)
                {
                    throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                }
            }

            Progress.CurrentProgress!.Report(2);
        }

        /// <summary>
        /// Fill the tiles array with tile objects containing the position of each tile
        /// </summary>
        private async Task PrepareObjects(CancellationToken cancellationToken)
        {
            log.LogDebug("Calculating triangles for map area");
            try
            {
                await Task.Run(Area.CalculateVerticesAndTriangles);
            }
            catch (TriangleGenerationException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.BorderNonSimple, e);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Progress.CurrentProgress!.Report(1);

            log.LogDebug("Populate Tiles Array");
            Tiles = new Tile[TotalTiles];

            log.LogDebug("This map is {width} tiles wide by {height} tiles high. {total} tiles in total", TilesWidth, TilesHeight, TotalTiles);

            await Task.Run(() =>
            {
                for (uint i = 0; i < Tiles.Length; i++)
                {
                    log.LogTrace("Processing tile {index} / {total}", i + 1, TotalTiles);

                    //Create new tile object at the correct top left corner location
                    Tiles[i] = new Tile(log, i, new Osgb36Coordinate(
                        TopLeft.Easting + ((i % TilesWidth) * MetersPerTile),
                        TopLeft.Northing - (Math.Floor((double)i / (double)TilesWidth) * MetersPerTile)
                    ), Scale, MetersPerTile);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            Progress.CurrentProgress!.Report(2);
        }

        private async Task CalculateGeographicalReferencingPolynomials(int polynomialSampleSize, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                _geographicalReferencingCoefficients = await Task.Run(() => PolynomialCalculator.Calculate(log, Progress.CurrentProgress!, TopLeft, BottomRight, polynomialSampleSize, PixelsPerMeter, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OutOfMemoryException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.PolynomialCalculationOutOfMemory, e);
            }
        }

        /// <summary>
        /// Download the required images for each tile
        /// </summary>
        private async Task DownloadRequiredImages(string? token, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Download Required Images");

            TileDownloader downloader = new TileDownloader(log, Progress.CurrentProgress!, Tiles, Area, Scale);

            try
            {
                await downloader.DownloadTilesAndGeneratePalette(token, cancellationToken);
            }
            catch (HttpRequestException e)
            {
                throw new MapGenerationException(MapGenerationExceptionReason.DownloadError, e);
            }
            _palette = downloader.Palette;

            _interpolationMatrix = GenerateInterpolationMatrix(_palette);
        }

        private byte[] GenerateInterpolationMatrix(Color[] palette)
        {
            log.LogDebug("Generating Interpolation Matrix");

            InterpolationMatrixCreator creator = new InterpolationMatrixCreator(palette, Scale);
            return creator.GetInterpolationMatrix();
        }

        /// <summary>
        /// For every tile, process the image, compress it, and add it to the QCT
        /// </summary>
        /// <param name="filePath">The path where the file should be saved</param>
        /// <param name="shouldOverwrite">Whether, if the file already exists, it should be overwritten</param>
        private async Task ProcessTiles(string filePath, bool shouldOverwrite, bool disableHardwareAccel, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Process Tiles");

            QctBuilder builder = new QctBuilder(log, Progress, _geographicalReferencingCoefficients, _palette, _interpolationMatrix, Metadata, TilesWidth, TilesHeight);

            await builder.Build(Tiles, Area, filePath, shouldOverwrite, disableHardwareAccel, cancellationToken);
        }
    }

    public enum MapGenerationExceptionReason
    {
        BorderOutOfBounds,
        BorderNonSimple,
        PolynomialCalculationOutOfMemory,
        DownloadError,
        OpenGLError,
        IOError
    }

    public class MapGenerationException : Exception
    {
        public MapGenerationExceptionReason Reason { get; }

        private static string ReasonToMessage(MapGenerationExceptionReason reason) => reason switch
        {
            MapGenerationExceptionReason.BorderOutOfBounds => "Map border is too far away from the UK. Bring the border closer to the UK.",
            MapGenerationExceptionReason.BorderNonSimple => "Map border is invalid. It must not cross over itself, must not have multiple points in the same location, and must not have 3 connected points in a straight line.",
            MapGenerationExceptionReason.PolynomialCalculationOutOfMemory => "The computer ran out of memory while calculating the geographical referencing polynomial coefficients. Try reducing the number of samples used.",
            MapGenerationExceptionReason.DownloadError => "An error occurred while downloading the images. Ordinance Survey have probably changed something on their website that broke this program. It's possible that this could be fixed by providing your own token.",
            MapGenerationExceptionReason.OpenGLError => "An OpenGL error occurred while processing the tiles. Check that your video drivers are up to date, or try disabling hardware acceleration.",
            MapGenerationExceptionReason.IOError => "The file could not be written to. Check the the file path provided is valid, and that the file is not already open in another program.",
            _ => throw new ArgumentException("Invalid value for reason")
        };

        public MapGenerationException(MapGenerationExceptionReason reason) : this(reason, null) { }

        public MapGenerationException(MapGenerationExceptionReason reason, Exception? innerException) : base(ReasonToMessage(reason), innerException)
        {
            Reason = reason;
        }
    }
}
