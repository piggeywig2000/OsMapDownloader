using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using OsMapDownloader.Cli.CommandOptions;
using OsMapDownloader.Coords;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct;

namespace OsMapDownloader
{
    class Program
    {
        static Task<int> Main(string[] args)
        {
            ParserResult<object> parseResult = new Parser(x => x.HelpWriter = null).ParseArguments<BoxOptions, PointsOptions>(args);
            return parseResult.MapResult(
                (BoxOptions opts) => Run(args, opts), //If it's successful, call Run
                (PointsOptions opts) => Run(args, opts),
                errs => Task.FromResult(DisplayHelp(parseResult)) //If it's not successful, call DisplayHelp
                );
        }

        static int DisplayHelp<T>(ParserResult<T> result)
        {
            Console.WriteLine(HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.AddNewLineBetweenHelpSections = true;
                h.MaximumDisplayWidth = 192;
                h.AddPreOptionsText(@"USAGE:
box <TopLeftLatitude> <TopLeftLongitude> <BottomRightLatitude> <BottomRightLongitude> <FilePath> [options...]
points <BorderPointsPath> <FilePath> [options...]");
                h.AddPostOptionsText(@"EXAMPLES:
box 53.985008 -1.161702 53.981299 -1.140141" + " \"area.qct\" -o --name=\"York\" --edition=\"2015\"" + @"
Download the city of York, saving the file with name area.qct in the current folder. Overwrite the file if it already exists.
Metadata tags: Name is York, Edition is 2015.

box 53.125098 -4.136918 53.114719 -4.110482" + " \"maps/holiday.qct\" -quiet --name=\"Summer Holiday 2020\" --long-name=\"Summer Holiday 2020 destination\" --revision=\"1\"" + @"
Download the village of Llanberis, saving the file with name holiday.qct in a folder called maps in the current folder. Only output error messages.
Metadata tags: Name is Summer Holiday 2020, Long Name is Summer Holiday 2020 destination, Revision is 1.

points" + " \"border.txt\" \"map.qct\" --scale=\"1:50000\" --name=\"Complex Map\"" + @"
Download a map with a border defined by latitude-longitude pairs found in border.txt, connected together.
The map is at 1:50000 scale (instead of the default 1:25000).");
                return h;
            }));
            return 1;
        }

        static ILogger CreateLoggerFromOptions(CommonOptions options) =>
            LoggerFactory.Create(logging => logging.AddConsole().AddDebug().SetMinimumLevel(options.Silent ? LogLevel.None : options.Quiet ? LogLevel.Warning : options.Debug ? LogLevel.Debug : options.Verbose ? LogLevel.Trace : LogLevel.Information)).CreateLogger("");

        static Task<int> Run(string[] args, BoxOptions options)
        {
            ILogger logger = CreateLoggerFromOptions(options);

            //Find top right and bottom left by converting to OSGB first, and constructing coords from those
            Wgs84Coordinate topLeft = new Wgs84Coordinate(options.TopLeftLongitude, options.TopLeftLatitude);
            Wgs84Coordinate bottomRight = new Wgs84Coordinate(options.BottomRightLongitude, options.BottomRightLatitude);
            Wgs84Coordinate topRight = new Osgb36Coordinate(bottomRight.ToOsgb36Accurate().Easting, topLeft.ToOsgb36Accurate().Northing).ToWgs84Accurate();
            Wgs84Coordinate bottomLeft = new Osgb36Coordinate(topLeft.ToOsgb36Accurate().Easting, bottomRight.ToOsgb36Accurate().Northing).ToWgs84Accurate();
            return Run(args, logger, new Wgs84Coordinate[] { topLeft, topRight, bottomRight, bottomLeft }, options);
        }

        static Task<int> Run(string[] args, PointsOptions options)
        {
            ILogger logger = CreateLoggerFromOptions(options);

            string pointsFile = File.ReadAllText(options.PointsPath);
            string[] pointsStr = pointsFile.Split(',', '\n', '\r', '\t', ' ').Where(str => !string.IsNullOrEmpty(str)).ToArray();
            double[] pointsDouble = new double[pointsStr.Length];
            for (int i = 0; i < pointsStr.Length; i++)
            {
                if (!double.TryParse(pointsStr[i], out double num))
                {
                    logger.LogCritical("{numStr} is not a valid number", pointsStr[i]);
                    Environment.Exit(1);
                }
                pointsDouble[i] = num;
            }
            if (pointsDouble.Length % 2 == 1)
            {
                logger.LogCritical("The border points file has an odd number of values. Since is should be latitude-longitude pairs, there must be an even number of values");
                Environment.Exit(1);
            }
            if (pointsDouble.Length < 6)
            {
                logger.LogCritical("The border points file has less than 3 points");
                Environment.Exit(1);
            }
            Wgs84Coordinate[] points = new Wgs84Coordinate[pointsDouble.Length / 2];
            for (int i = 0; i < pointsDouble.Length; i += 2)
            {
                points[i / 2] = new Wgs84Coordinate(pointsDouble[i + 1], pointsDouble[i]);
            }
            return Run(args, logger, points, options);
        }

        static async Task<int> Run(string[] args, ILogger logger, Wgs84Coordinate[] borderPoints, CommonOptions options)
        {
            //Catch overflow exceptions
            try
            {
                Map map = new Map(logger,
                borderPoints,
                options.Scale,
                new QctMetadata()
                {
                    FileType = QctType.QuickChartMap,
                    LongTitle = options.LongName,
                    Name = options.Name,
                    Identifier = options.Identifier,
                    Edition = options.Edition,
                    Revision = options.Revision,
                    Keywords = options.Keywords,
                    Copyright = options.Copyright,
                    Scale = options.MetadataScale,
                    Datum = options.Datum,
                    Depths = options.Depths,
                    Heights = options.Heights,
                    Projection = options.Projection,
                    Flags = 0,
                    WriteOriginalFileName = false,
                    WriteOriginalFileSize = false,
                    WriteOriginalCreationTime = false,
                    MapType = options.MapType,
                    DatumShiftNorth = 0.0,
                    DatumShiftEast = 0.0
                });
                map.Progress.ProgressChanged += (s, e) => LogProgressChange(map, logger);
                LogProgressChange(map, logger);
                await map.GenerateQCTFileFromMap(options.DestinationPath, options.Overwrite, options.PolynomialSampleSize, options.Token, options.KeepTiles, options.DisableHardwareAccel);
            }
            catch (MapGenerationException e)
            {
                logger.LogCritical(e.Reason switch
                {
                    MapGenerationExceptionReason.BorderOutOfBounds => "A point in the map border is too far away from the UK. Try moving far away points closer to the UK, and try again.",
                    MapGenerationExceptionReason.BorderNonSimple => "The map border is an invalid shape.\n\nCheck that:\n• The border does not cross over itself\n• There aren't two points in the same location\n• There aren't 3 points connected to each other in a perfectly straight line",
                    MapGenerationExceptionReason.PolynomialCalculationOutOfMemory => "The system ran out of memory while calculating the geographical referencing polynomial coefficients. Try reducing the polynomial sample size using the --polynomial-sample-size option and try again.",
                    MapGenerationExceptionReason.DownloadError => "An error occurred while downloading the images. Ordinance Survey have probably changed something on their website that broke this program.\n\nIt's possible that this could be fixed by providing your own download token using the --token option.",
                    MapGenerationExceptionReason.OpenGLError => "An OpenGL error occurred while processing the tiles.\n\nCheck that your video drivers are up to date. If they are up to date and this error still occurs, try disabling hardware acceleration using the --disable-hw-accel option.",
                    MapGenerationExceptionReason.IOError => "The file could not be written to.\n\nCheck that the save location provided is a valid folder. If the file is being overwritten, make sure that the file is not open in another program.",
                    _ => throw new ArgumentException("Invalid value for map generation exception reason")
                });
                return 1;
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "An unknown error occurred while exporting. Error details are provided below:");
                return 2;
            }
            return 0;
        }

        private static void LogProgressChange(Map map, ILogger logger)
        {
            if (map.Progress.IsCompleted)
            {
                logger.LogInformation("Done");
            }
            else
            {
                logger.LogInformation("{name}: {percentage}%\n{status}", map.Progress.CurrentProgressItem!.Name, (map.Progress.CurrentProgressItem!.Value * 100).ToString("0.0000"), map.Progress.CurrentProgressItem!.Status);
            }
        }
    }
}