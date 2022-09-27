using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using OsMapDownloader.Border;
using OsMapDownloader.Progress;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace OsMapDownloader.Qct
{
    public class QctBuilder
    {
        private readonly ILogger log;
        private readonly ProgressTracker progress;

        /// <summary>
        /// The path that this builder writes to
        /// </summary>
        public string QCTFilePath { get; private set; } = "";

        /// <summary>
        /// The geographical referencing coefficients
        /// </summary>
        public QctGeographicalReferencingCoefficients GeographicalReferencingCoefficients { get; set; }

        /// <summary>
        /// The colour palette to use. Must be of length 128
        /// </summary>
        public Color[] Palette { get; set; }

        /// <summary>
        /// The interpolation matrix to use. Must be of length 16384
        /// </summary>
        public byte[] InterpolationMatrix { get; set; }

        /// <summary>
        /// Metadata for the QCT file
        /// </summary>
        public QctMetadata Metadata { get; set; }

        /// <summary>
        /// The width (tiles) of this QCT file
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// The height (tiles) of this QCT file
        /// </summary>
        public uint Height { get; set; }



        /// <summary>
        /// Create a new QCT builder with empty values
        /// </summary>
        public QctBuilder(ILogger logger, ProgressTracker progress, QctGeographicalReferencingCoefficients geographicalReferencingCoefficients, Color[] palette, byte[] interpolationMatrix, QctMetadata metadata, uint width, uint height)
        {
            log = logger;
            this.progress = progress;
            GeographicalReferencingCoefficients = geographicalReferencingCoefficients;
            Palette = palette;
            InterpolationMatrix = interpolationMatrix;
            Metadata = metadata;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Begin building asyncronously to the provided path
        /// </summary>
        /// <param name="tiles">The tiles to write to the map</param>
        /// <param name="newQctFilePath">The path to write the new file to</param>
        /// <param name="shouldOverwrite">Whether, if the file already exists, it should be overwritten</param>
        /// <returns>A Task representing the operation</returns>
        public async Task Build(Tile[] tiles, MapArea area, string newQctFilePath, bool shouldOverwrite, bool disableHardwareAccel, CancellationToken cancellationToken = default(CancellationToken))
        {
            FileStream fs;
            try
            {
                fs = CreateFile(newQctFilePath, shouldOverwrite);
            }
            catch (Exception e)
            {
                if (e is IOException || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException)
                {
                    throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                }
                throw;
            }

            //Write null placeholder for the metadata and geographical referencing coordinates
            fs.Seek(0x01A0, SeekOrigin.Begin);

            await WritePaletteAndInterpolationMatrix(fs, cancellationToken);
            if (disableHardwareAccel)
            {
                await WriteTiles(fs, tiles, area, null, cancellationToken);
            }
            else
            {
                //Create in separate thread since OpenGL will do a bit of blocking
                //If OpenGL messes up it might be because it's not on the main thread, but it hopefully won't since we don't use events
                log.LogDebug("Creating OpenGL processing thread");
                await Task.Run(() =>
                {
                    try
                    {
                        log.LogTrace("Creating OpenGL window");
                        using OpenGLManager glManager = new OpenGLManager();
                        log.LogTrace("Initialising OpenGL window");
                        glManager.Init(area);

                        Task writeTilesTask = WriteTiles(fs, tiles, area, glManager, cancellationToken);
                        glManager.ProcessTilesUntilTaskComplete(writeTilesTask);

                        glManager.Close();
                    }
                    catch (Exception e)
                    {
                        if (e is OpenTK.Core.Platform.PlatformException || e is OpenTK.Windowing.GraphicsLibraryFramework.GLFWException)
                        {
                            throw new MapGenerationException(MapGenerationExceptionReason.OpenGLError, e);
                        }
                        throw;
                    }
                });
            }

            await WriteMetadata(fs, cancellationToken);
            await WriteGeographicalReferencingPolynomials(fs, cancellationToken);
            await fs.FlushAsync(cancellationToken);
            fs.Close();
            await fs.DisposeAsync();
            progress.CurrentProgress!.Report(1);
        }

        private FileStream CreateFile(string path, bool overwrite)
        {
            if (QCTFilePath != "")
                throw new InvalidOperationException("There is already a build in progress");

            QCTFilePath = path;

            //Validate metadata
            if (Width == 0 || Height == 0)
                throw new InvalidOperationException("The width and height must both be greater than 0");

            //Create the new file
            FileStream fs = new FileStream(QCTFilePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None);
            return fs;
        }

        private async Task WritePaletteAndInterpolationMatrix(FileStream fs, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Writing palette");
            if (Palette.Length > 128)
                throw new InvalidOperationException("The palette must be of length 128 or less");
            byte[] paletteBuffer = new byte[1024];
            for (int i = 0; i < Palette.Length; i++)
            {
                Rgb24 thisColour = Palette[i];
                paletteBuffer[i * 4] = thisColour.B;
                paletteBuffer[i * 4 + 1] = thisColour.G;
                paletteBuffer[i * 4 + 2] = thisColour.R;
            }
            await fs.WriteAsync(paletteBuffer, 0, 1024, cancellationToken);

            if (InterpolationMatrix.Length != 16384)
                throw new InvalidOperationException("The interpolation matrix must be of length 16384");
            log.LogDebug("Writing interpolation matrix");
            await fs.WriteAsync(InterpolationMatrix, 0, 16384, cancellationToken);
        }

        private async Task WriteTiles(FileStream fs, Tile[] tiles, MapArea area, OpenGLManager? glManager, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Writing tiles");

            int capacityPerBlock = Environment.ProcessorCount * 4;
            using IQuantizer<Rgba32> quantizer = new PaletteQuantizer(new ReadOnlyMemory<Color>(Palette), new QuantizerOptions() { Dither = null }).CreatePixelSpecificQuantizer<Rgba32>(new Configuration());

            //Create DataFlow blocks
            TransformBlock<Tile, (Tile, Image<Rgba32>?[])> loadBlock = new TransformBlock<Tile, (Tile, Image<Rgba32>?[])>(
                async tile => (tile, await tile.LoadImages()),
                new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, EnsureOrdered = true, BoundedCapacity = capacityPerBlock, CancellationToken = cancellationToken });

            TransformBlock<(Tile, Image<Rgba32>?[]), (Tile, byte[])> processBlock;
            if (glManager == null)
            {
                processBlock = new TransformBlock<(Tile, Image<Rgba32>?[]), (Tile, byte[])>(
                    ((Tile tile, Image<Rgba32>?[] images) tuple) => (tuple.tile, tuple.tile.ProcessImageSW(tuple.images, area)),
                    new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, EnsureOrdered = true, BoundedCapacity = capacityPerBlock, CancellationToken = cancellationToken });
            }
            else
            {
                //Find the maximum width and height for all tiles' web tiles
                int maxWidth = 0;
                int maxHeight = 0;
                foreach (Tile tile in tiles)
                {
                    maxWidth = Math.Max(maxWidth, tile.WebTileWidth);
                    maxHeight = Math.Max(maxHeight, tile.WebTileHeight);
                }

                processBlock = new TransformBlock<(Tile, Image<Rgba32>?[]), (Tile, byte[])>(
                    async ((Tile tile, Image<Rgba32>?[] images) tuple) => (tuple.tile, await tuple.tile.ProcessImageHW(tuple.images, glManager, maxWidth, maxHeight)),
                    new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, EnsureOrdered = true, BoundedCapacity = capacityPerBlock, CancellationToken = cancellationToken });
            }

            TransformBlock<(Tile, byte[]), byte[]> compressBlock = new TransformBlock<(Tile, byte[]), byte[]>(
                    ((Tile tile, byte[] image) tuple) => tuple.tile.ConvertToPaletteAndCompress(tuple.image, quantizer),
                    new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount, EnsureOrdered = true, BoundedCapacity = capacityPerBlock, CancellationToken = cancellationToken });

            uint tilesWritten = 0;
            uint totalBytesWritten = 0;
            ActionBlock<byte[]> writeBlock = new ActionBlock<byte[]>(async (data) =>
            {
                log.LogDebug("Writing tile {tileId} / {total}", tilesWritten + 1, Width * Height);

                //The pointer should point to the end of the file (the offset of index pointers plus size of index pointers plus bytes written)
                uint pointerLocation = 0x45A0 + Width * Height * 4 + totalBytesWritten;

                //Firstly write the tile data to the pointer location
                fs.Seek(pointerLocation, SeekOrigin.Begin);
                await fs.WriteAsync(data, cancellationToken);

                //Next write the pointer to the image index pointers array
                fs.Seek(0x45A0 + tilesWritten * 4, SeekOrigin.Begin);
                await fs.WriteAsync(BitConverter.GetBytes(pointerLocation).AsMemory(0, 4), cancellationToken);

                //Finally, increase the tiles counter and bytes counter
                tilesWritten++;
                totalBytesWritten += (uint)data.Length;
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, EnsureOrdered = true, BoundedCapacity = capacityPerBlock, CancellationToken = cancellationToken });

            loadBlock.LinkTo(processBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            processBlock.LinkTo(compressBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            compressBlock.LinkTo(writeBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            //Create update posting task
            Task updatePoster = new Task(async () =>
            {
                //Start posting updates until we're done
                ((ProgressCollection)progress.CurrentProgressItem!).TotalItems = (uint)tiles.Length;
                do
                {
                    await Task.WhenAny(writeBlock.Completion, Task.Delay(1000));

                    log.LogTrace("Load: {load}    LoadOutput: {loadOutput}    Process: {process}    ProcessOutput: {processOutput}    Compress: {compress}    CompressOutput: {compressOutput}    Write: {write}", loadBlock.InputCount, loadBlock.OutputCount, processBlock.InputCount, processBlock.OutputCount, compressBlock.InputCount, compressBlock.OutputCount, writeBlock.InputCount);
                    progress.CurrentProgress!.Report(tilesWritten);
                } while (!writeBlock.Completion.IsCompleted);
            });
            updatePoster.Start();

            //Post tiles to processBlock
            foreach (Tile tile in tiles)
            {
                await loadBlock.SendAsync(tile, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            loadBlock.Complete();
            await writeBlock.Completion;
            await updatePoster;
        }

        private async Task WriteMetadata(FileStream fs, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Writing metadata");

            //Firstly figure out where new references will go
            uint newPointerLocation = (uint)fs.Length;

            //Now write the metadata

            //Type (magic number)
            await fs.WriteIntegerMetadata(0x00, (uint)Metadata.FileType, cancellationToken);

            //File format version
            await fs.WriteIntegerMetadata(0x04, 0x00000002, cancellationToken);

            //Width
            await fs.WriteIntegerMetadata(0x08, Width, cancellationToken);

            //Height
            await fs.WriteIntegerMetadata(0x0C, Height, cancellationToken);

            //Long Title
            newPointerLocation = await fs.WriteStringMetadata(0x10, Metadata.LongTitle, newPointerLocation, cancellationToken);

            //Name
            newPointerLocation = await fs.WriteStringMetadata(0x14, Metadata.Name, newPointerLocation, cancellationToken);

            //Identifier
            newPointerLocation = await fs.WriteStringMetadata(0x18, Metadata.Identifier, newPointerLocation, cancellationToken);

            //Edition
            newPointerLocation = await fs.WriteStringMetadata(0x1C, Metadata.Edition, newPointerLocation, cancellationToken);

            //Revision
            newPointerLocation = await fs.WriteStringMetadata(0x20, Metadata.Revision, newPointerLocation, cancellationToken);

            //Keywords
            newPointerLocation = await fs.WriteStringMetadata(0x24, Metadata.Keywords, newPointerLocation, cancellationToken);

            //Copyright
            newPointerLocation = await fs.WriteStringMetadata(0x28, Metadata.Copyright, newPointerLocation, cancellationToken);

            //Scale
            newPointerLocation = await fs.WriteStringMetadata(0x2C, Metadata.Scale, newPointerLocation, cancellationToken);

            //Datum
            newPointerLocation = await fs.WriteStringMetadata(0x30, Metadata.Datum, newPointerLocation, cancellationToken);

            //Depths
            newPointerLocation = await fs.WriteStringMetadata(0x34, Metadata.Depths, newPointerLocation, cancellationToken);

            //Heights
            newPointerLocation = await fs.WriteStringMetadata(0x38, Metadata.Heights, newPointerLocation, cancellationToken);

            //Projection
            newPointerLocation = await fs.WriteStringMetadata(0x3C, Metadata.Projection, newPointerLocation, cancellationToken);

            //Flags
            await fs.WriteIntegerMetadata(0x40, (uint)Metadata.Flags, cancellationToken);

            //Original File Name
            if (Metadata.WriteOriginalFileName)
                newPointerLocation = await fs.WriteStringMetadata(0x44, Path.GetFileName(QCTFilePath), newPointerLocation, cancellationToken);
            else
                await fs.WriteIntegerMetadata(0x44, 0, cancellationToken);

            //Original File Size
            if (Metadata.WriteOriginalFileSize)
                throw new NotImplementedException("Writing the file size is too much like hard work");
            else
                await fs.WriteIntegerMetadata(0x48, 0, cancellationToken);

            //Original File Creation Time
            if (Metadata.WriteOriginalCreationTime)
                await fs.WriteIntegerMetadata(0x4C, (uint)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds, cancellationToken);
            else
                await fs.WriteIntegerMetadata(0x4C, 0, cancellationToken);

            //Reserved
            await fs.WriteIntegerMetadata(0x50, 0, cancellationToken);

            //Extended data structure
            //Create pointer to extended data structure
            await fs.WriteIntegerMetadata(0x54, newPointerLocation, cancellationToken);
            //Remember where the extended data structure is located - we'll need it
            uint edsBeginLocation = newPointerLocation;
            //Increase newPointerLocation by 0x20 to make space for it
            newPointerLocation += 0x20;

            //EDS Map Type
            newPointerLocation = await fs.WriteStringMetadata(edsBeginLocation + 0x00, Metadata.MapType, newPointerLocation, cancellationToken);

            //EDS Datum Shift
            newPointerLocation = await fs.WriteDoubleArrayMetadata(edsBeginLocation + 0x04, new double[] { Metadata.DatumShiftNorth, Metadata.DatumShiftEast }, newPointerLocation, cancellationToken);

            //Disk Name
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x08, 0, cancellationToken);

            //Reserved
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x0C, 0, cancellationToken);

            //Reserved
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x10, 0, cancellationToken);

            //Licence Information
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x14, 0, cancellationToken);

            //Associated Data
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x18, 0, cancellationToken);

            //Digital Map Shop
            await fs.WriteIntegerMetadata(edsBeginLocation + 0x1C, 0, cancellationToken);

            //Number of map outline points
            await fs.WriteIntegerMetadata(0x58, (uint)Metadata.MapOutline.Length, cancellationToken);

            //Map outline
            double[] mapOutlineD = new double[Metadata.MapOutline.Length * 2];
            for (int i = 0; i < Metadata.MapOutline.Length; i++)
            {
                mapOutlineD[i * 2] = Metadata.MapOutline[i].Latitude;
                mapOutlineD[i * 2 + 1] = Metadata.MapOutline[i].Longitude;
            }
            newPointerLocation = await fs.WriteDoubleArrayMetadata(0x5C, mapOutlineD, newPointerLocation, cancellationToken);
        }

        private async Task WriteGeographicalReferencingPolynomials(FileStream fs, CancellationToken cancellationToken = default(CancellationToken))
        {
            log.LogDebug("Writing geographical referencing coefficients");
            await fs.WriteDoubleMetadata(0x060, GeographicalReferencingCoefficients.Eas, cancellationToken);
            await fs.WriteDoubleMetadata(0x068, GeographicalReferencingCoefficients.EasY, cancellationToken);
            await fs.WriteDoubleMetadata(0x070, GeographicalReferencingCoefficients.EasX, cancellationToken);
            await fs.WriteDoubleMetadata(0x078, GeographicalReferencingCoefficients.EasYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x080, GeographicalReferencingCoefficients.EasXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x088, GeographicalReferencingCoefficients.EasXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x090, GeographicalReferencingCoefficients.EasYYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x098, GeographicalReferencingCoefficients.EasYYX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0A0, GeographicalReferencingCoefficients.EasYXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0A8, GeographicalReferencingCoefficients.EasXXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0B0, GeographicalReferencingCoefficients.Nor, cancellationToken);
            await fs.WriteDoubleMetadata(0x0B8, GeographicalReferencingCoefficients.NorY, cancellationToken);
            await fs.WriteDoubleMetadata(0x0C0, GeographicalReferencingCoefficients.NorX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0C8, GeographicalReferencingCoefficients.NorYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x0D0, GeographicalReferencingCoefficients.NorXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x0D8, GeographicalReferencingCoefficients.NorXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0E0, GeographicalReferencingCoefficients.NorYYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x0E8, GeographicalReferencingCoefficients.NorYYX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0F0, GeographicalReferencingCoefficients.NorYXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x0F8, GeographicalReferencingCoefficients.NorXXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x100, GeographicalReferencingCoefficients.Lat, cancellationToken);
            await fs.WriteDoubleMetadata(0x108, GeographicalReferencingCoefficients.LatX, cancellationToken);
            await fs.WriteDoubleMetadata(0x110, GeographicalReferencingCoefficients.LatY, cancellationToken);
            await fs.WriteDoubleMetadata(0x118, GeographicalReferencingCoefficients.LatXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x120, GeographicalReferencingCoefficients.LatXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x128, GeographicalReferencingCoefficients.LatYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x130, GeographicalReferencingCoefficients.LatXXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x138, GeographicalReferencingCoefficients.LatXXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x140, GeographicalReferencingCoefficients.LatXYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x148, GeographicalReferencingCoefficients.LatYYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x150, GeographicalReferencingCoefficients.Lon, cancellationToken);
            await fs.WriteDoubleMetadata(0x158, GeographicalReferencingCoefficients.LonX, cancellationToken);
            await fs.WriteDoubleMetadata(0x160, GeographicalReferencingCoefficients.LonY, cancellationToken);
            await fs.WriteDoubleMetadata(0x168, GeographicalReferencingCoefficients.LonXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x170, GeographicalReferencingCoefficients.LonXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x178, GeographicalReferencingCoefficients.LonYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x180, GeographicalReferencingCoefficients.LonXXX, cancellationToken);
            await fs.WriteDoubleMetadata(0x188, GeographicalReferencingCoefficients.LonXXY, cancellationToken);
            await fs.WriteDoubleMetadata(0x190, GeographicalReferencingCoefficients.LonXYY, cancellationToken);
            await fs.WriteDoubleMetadata(0x198, GeographicalReferencingCoefficients.LonYYY, cancellationToken);
        }
    }
}
