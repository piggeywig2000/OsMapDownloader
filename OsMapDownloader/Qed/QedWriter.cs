using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsMapDownloader.Border;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct;
using Serilog;

namespace OsMapDownloader.Qed
{
    internal class QedWriter
    {
        private readonly ProgressTracker progress;

        /// <summary>
        /// The path that this writer writes to
        /// </summary>
        public string QEDFilePath { get; private set; } = "";

        /// <summary>
        /// The geographical referencing coefficients
        /// </summary>
        public GeographicalReferencingCoefficients GeographicalReferencingCoefficients { get; set; }

        /// <summary>
        /// The width (tiles) of this QED file
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// The height (tiles) of this QED file
        /// </summary>
        public uint Height { get; set; }

        /// <summary>
        /// Height and width of each tile
        /// </summary>
        public uint PointsPerTile { get; set; }

        /// <summary>
        /// Width of each tile in meters
        /// </summary>
        public double MetersPerTile { get; set; }

        /// <summary>
        /// Meters between each point
        /// </summary>
        public double MetersBetweenPoints { get; set; }

        public QedWriter(ProgressTracker progress, GeographicalReferencingCoefficients geographicalReferencingCoefficients, uint width, uint height, uint pointsPerTile, double metersPerTile, double metersBetweenPoints)
        {
            this.progress = progress;
            GeographicalReferencingCoefficients = geographicalReferencingCoefficients;
            Width = width;
            Height = height;
            PointsPerTile = pointsPerTile;
            MetersPerTile = metersPerTile;
            MetersBetweenPoints = metersBetweenPoints;
        }

        public async Task Write(Tile[] tiles, MapArea area, string newQedFilePath, bool shouldOverwrite, CancellationToken cancellationToken = default(CancellationToken))
        {
            FileStream fs;
            try
            {
                fs = CreateFile(newQedFilePath, shouldOverwrite);
            }
            catch (Exception e)
            {
                if (e is IOException || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException)
                {
                    throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                }
                throw;
            }

            await WriteMetadata(fs, cancellationToken);
            await GeographicalReferencingCoefficients.Write(fs, 0x18, cancellationToken);

            await WriteTiles(fs, tiles, area);

            await fs.FlushAsync(cancellationToken);
            fs.Close();
            await fs.DisposeAsync();
        }

        private FileStream CreateFile(string path, bool overwrite)
        {
            if (QEDFilePath != "")
                throw new InvalidOperationException("There is already a build in progress");

            QEDFilePath = path;

            //Validate metadata
            if (Width == 0 || Height == 0)
                throw new InvalidOperationException("The width and height must both be greater than 0");

            //Create the new file
            FileStream fs = new FileStream(QEDFilePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None);
            return fs;
        }

        private async Task WriteMetadata(FileStream fs, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Debug("Writing metadata");

            //Type (magic number)
            await fs.WriteIntegerMetadata(0x00, 0x34EB3498, cancellationToken);

            //0 (not sure what this is)
            await fs.WriteIntegerMetadata(0x04, 0, cancellationToken);

            //Height / width of each tile in data points
            await fs.WriteIntegerMetadata(0x08, PointsPerTile, cancellationToken);

            //Height
            await fs.WriteIntegerMetadata(0x0C, Height, cancellationToken);

            //Width
            await fs.WriteIntegerMetadata(0x10, Width, cancellationToken);

            //Name (set to 0 for no name, doesn't seem to be used anyway)
            await fs.WriteIntegerMetadata(0x14, 0, cancellationToken);
        }

        private async Task WriteTiles(FileStream fs, Tile[] tiles, MapArea area, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Run(async () =>
            {
                using TerrainDataReader dataReader = new TerrainDataReader("terrain50.zip");

                uint pointerStart = 0x158;
                uint dataStart = pointerStart + ((uint)tiles.Length * 4);

                ((ProgressCollection)progress.CurrentProgress!).TotalItems = tiles.Length;
                int completedTiles = 0;
                DateTime timeStarted = DateTime.UtcNow;
                TimeSpan nextUpdate = TimeSpan.FromSeconds(1);

                foreach (Tile tile in tiles)
                {
                    if (area.IsRectangleInArea(tile.TopLeft.Easting - MetersBetweenPoints, tile.TopLeft.Northing + MetersBetweenPoints, tile.TopLeft.Easting + MetersPerTile + MetersBetweenPoints, tile.TopLeft.Northing - MetersPerTile - MetersBetweenPoints))
                    {
                        //Fetch data
                        int[] source = await tile.GetData(dataReader, PointsPerTile, MetersBetweenPoints, cancellationToken);

                        //Compress data
                        List<byte> compressed = new List<byte>();
                        short baseHeight = (short)source.Min();
                        byte bitsPerItem = (byte)Math.Ceiling(Math.Log2(source.Select(point => point - baseHeight).Max()));
                        compressed.Add(bitsPerItem);
                        compressed.AddRange(BitConverter.GetBytes(baseHeight));
                        if (bitsPerItem > 0)
                        {
                            int packArrayLength = (int)(PointsPerTile * PointsPerTile * bitsPerItem);
                            BitArray packArray = new BitArray(packArrayLength);
                            foreach (int height in source)
                            {
                                //Shuffle previous contents up
                                packArray.RightShift(bitsPerItem);

                                //Stick some new contents on the end
                                int offset = height - baseHeight;
                                for (int i = 0; i < bitsPerItem; i++)
                                {
                                    packArray[packArrayLength - bitsPerItem + i] = (offset & (1 << i)) != 0;
                                }
                            }
                            byte[] packBuffer = new byte[(int)Math.Ceiling(packArrayLength / 8.0)];
                            packArray.CopyTo(packBuffer, 0);
                            compressed.AddRange(packBuffer);
                        }

                        //Write data
                        await fs.WriteIntegerMetadata(pointerStart, dataStart, cancellationToken);
                        fs.Seek(dataStart, SeekOrigin.Begin);
                        await fs.WriteAsync(compressed.ToArray(), cancellationToken);
                        dataStart += (uint)compressed.Count;
                    }
                    else
                    {
                        await fs.WriteIntegerMetadata(pointerStart, 0, cancellationToken);
                    }
                    pointerStart += 4;

                    completedTiles++;
                    if (DateTime.UtcNow >= timeStarted + nextUpdate && completedTiles < tiles.Length)
                    {
                        progress.CurrentProgress!.Report(completedTiles);
                        nextUpdate = nextUpdate.Add(TimeSpan.FromSeconds(1));
                    }
                }

                progress.CurrentProgress!.Report(tiles.Length);
            });
        }
    }
}
