//#define DEBUG_PROCESS

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsMapDownloader.Border;
using OsMapDownloader.CompressionMethod;
using OsMapDownloader.Coords;
using OsMapDownloader.WebDownloader;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace OsMapDownloader
{
    public class Tile
    {
        private readonly ILogger log;
        
        public uint Id { get; }

        public Osgb36Coordinate TopLeft { get; }
        public Osgb36Coordinate BottomRight { get; }
        public Wgs84Coordinate LatlongTopLeft { get; }
        public Wgs84Coordinate LatlongBottomRight { get; }
        public Wgs84Coordinate LatlongTopRight { get; }
        public Wgs84Coordinate LatlongBottomLeft { get; }

        public Scale MapScale { get; }

        public int WebTileMinX { get; private set; }
        public int WebTileMaxX { get; private set; }
        public int WebTileMinY { get; private set; }
        public int WebTileMaxY { get; private set; }
        public int WebTileWidth { get => WebTileMaxX - WebTileMinX + 1; }
        public int WebTileHeight { get => WebTileMaxY - WebTileMinY + 1; }

        /// <summary>
        /// Create a new Tile with the specified top left location.
        /// </summary>
        /// <param name="topLeftCorner">The location of the top left corner</param>
        public Tile(ILogger logger, uint id, Osgb36Coordinate topLeftCorner, Scale mapScale, double metersPerTile)
        {
            log = logger;
            Id = id;
            TopLeft = topLeftCorner;
            BottomRight = new Osgb36Coordinate(topLeftCorner.Easting + metersPerTile, topLeftCorner.Northing - metersPerTile);
            LatlongTopLeft = TopLeft.ToWgs84Accurate();
            LatlongBottomRight = BottomRight.ToWgs84Accurate();
            LatlongTopRight = new Osgb36Coordinate(BottomRight.Easting, TopLeft.Northing).ToWgs84Accurate();
            LatlongBottomLeft = new Osgb36Coordinate(TopLeft.Easting, BottomRight.Northing).ToWgs84Accurate();
            MapScale = mapScale;
        }

        public void GetRequiredWebTileRange()
        {
            const double ERROR_MARGIN = 0.001;

            //Make a web tile instance for each of the 4 corners
            WebTile tl = new WebTile(LatlongTopLeft + new Wgs84Coordinate(-ERROR_MARGIN, +ERROR_MARGIN), MapScale);
            WebTile tr = new WebTile(LatlongTopRight + new Wgs84Coordinate(+ERROR_MARGIN, +ERROR_MARGIN), MapScale);
            WebTile bl = new WebTile(LatlongBottomLeft + new Wgs84Coordinate(-ERROR_MARGIN, -ERROR_MARGIN), MapScale);
            WebTile br = new WebTile(LatlongBottomRight + new Wgs84Coordinate(+ERROR_MARGIN, -ERROR_MARGIN), MapScale);

            //Get the web tile min and max x and y
            WebTileMinX = (tl.TopLeft.X < bl.TopLeft.X) ? tl.TopLeft.X : bl.TopLeft.X;
            WebTileMaxX = (tr.TopLeft.X > br.TopLeft.X) ? tr.TopLeft.X : br.TopLeft.X;
            WebTileMinY = (tl.TopLeft.Y < tr.TopLeft.Y) ? tl.TopLeft.Y : tr.TopLeft.Y;
            WebTileMaxY = (bl.TopLeft.Y > br.TopLeft.Y) ? bl.TopLeft.Y : br.TopLeft.Y;
        }

        public async Task<Image<Rgba32>?[]> LoadImages()
        {
            Image<Rgba32>?[] images = new Image<Rgba32>[WebTileWidth * WebTileHeight];
            int currentX = WebTileMinX;
            int currentY = WebTileMinY;
            int i = 0;
            while (currentY <= WebTileMaxY)
            {
                WebTile currentTile = new WebTile(currentX, currentY, MapScale);
                Image<Rgba32>? thisImage = null;
                if (File.Exists("working/" + currentTile.FileName + ".png"))
                {
                    int attemptsRemaining = 100;
                    while (thisImage == null)
                    {
                        try
                        {
                            thisImage = await Image.LoadAsync<Rgba32>("working/" + currentTile.FileName + ".png");
                        }
                        catch (IOException e)
                        {
                            if (attemptsRemaining > 0)
                            {
                                await Task.Delay(10); //Try again in a bit
                            }
                            else
                            {
                                throw new MapGenerationException(MapGenerationExceptionReason.IOError, e);
                            }
                        }
                    }
                }
                images[i] = thisImage;

                i++;
                currentX++;
                if (currentX > WebTileMaxX)
                {
                    currentX = WebTileMinX;
                    currentY++;
                }
            }

            return images;
        }

        private Image<Rgba32> JoinImages(Image<Rgba32>?[] images, int? width = null, int? height = null)
        {
            //Join images together
            Image<Rgba32> baseCanvas = new Image<Rgba32>(new Configuration(new PngConfigurationModule()), width ?? WebTileWidth * 256, height ?? WebTileHeight * 256, new Rgba32(0, 0, 0, 0));
            int currentX = WebTileMinX;
            int currentY = WebTileMinY;
            int i = 0;
            while (currentY <= WebTileMaxY)
            {
                WebTile currentTile = new WebTile(currentX, currentY, MapScale);
                Image<Rgba32>? thisImage = images[i];
                //If the image is null, the tile doesn't exist. If we don't copy anything, it'll just be left as transparent
                if (thisImage != null)
                {
                    int xOffset = (currentX - WebTileMinX) * 256;
                    int yOffset = (currentY - WebTileMinY) * 256;
                    for (int row = 0; row < 256; row++)
                    {
                        thisImage.Frames[0].PixelBuffer.DangerousGetRowSpan(row).CopyTo(baseCanvas.Frames[0].PixelBuffer.DangerousGetRowSpan(row + yOffset).Slice(xOffset, 256));
                    }

                    thisImage.Dispose();
                }

                i++;
                currentX++;
                if (currentX > WebTileMaxX)
                {
                    currentX = WebTileMinX;
                    currentY++;
                }
            }

#if DEBUG_PROCESS
            baseCanvas.SaveAsBmp("working/" + TopLeft.Northing + "_" + TopLeft.Easting + ".joined.bmp", new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
#endif

            return baseCanvas;
        }

        /// <summary>
        /// Get the amount that ordinance survey rotated the image
        /// </summary>
        /// <returns>The rotation in radians</returns>
        private double GetRotationError(Coordinate topLeftPixels, Coordinate bottomRightPixels)
        {
            return Math.Atan2(bottomRightPixels.Y - topLeftPixels.Y, bottomRightPixels.X - topLeftPixels.X) - (Math.PI / 4);
        }

        /// <summary>
        /// Stitch together the downloaded tiles, rotate them, and crop them
        /// </summary>
        /// <returns>The final image. 64x64 pixels</returns>
        public byte[] ProcessImageSW(Image<Rgba32>?[] images, MapArea area)
        {
            Image<Rgba32> image = JoinImages(images);

            //Get pixel location of top left (256 pixels per tile)
            int tlImageX = WebTileMinX * 256;
            int tlImageY = WebTileMinY * 256;

            //Get the exact pixel location of the tile corners in the image
            Coordinate tlTile = WebTile.GetPixelsFromLonLatExact(LatlongTopLeft, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate brTile = WebTile.GetPixelsFromLonLatExact(LatlongBottomRight, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate trTile = WebTile.GetPixelsFromLonLatExact(LatlongTopRight, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate blTile = WebTile.GetPixelsFromLonLatExact(LatlongBottomLeft, WebTile.GetMaxZoomFromScale(MapScale));

            //Get the width and height of the tile on the canvas
            double tileWidth = Math.Sqrt(Math.Pow(trTile.X - tlTile.X, 2) + Math.Pow(trTile.Y - tlTile.Y, 2));
            double tileHeight = Math.Sqrt(Math.Pow(blTile.X - tlTile.X, 2) + Math.Pow(blTile.Y - tlTile.Y, 2));

#if DEBUG_PROCESS
            IPath rectanglePath = new PathBuilder()
            .AddLine((float)(tlTile.X - tlImageX), (float)(tlTile.Y - tlImageY), (float)(trTile.X - tlImageX), (float)(trTile.Y - tlImageY))
            .AddLine((float)(trTile.X - tlImageX), (float)(trTile.Y - tlImageY), (float)(brTile.X - tlImageX), (float)(brTile.Y - tlImageY))
            .AddLine((float)(brTile.X - tlImageX), (float)(brTile.Y - tlImageY), (float)(blTile.X - tlImageX), (float)(blTile.Y - tlImageY))
            .AddLine((float)(blTile.X - tlImageX), (float)(blTile.Y - tlImageY), (float)(tlTile.X - tlImageX), (float)(tlTile.Y - tlImageY))
            .Build();
            image.Mutate(img => img.Draw(Color.LimeGreen, 1.0f, rectanglePath));
#endif


            //Get the centre of the tile
            double tileCentreX = tlTile.X + ((brTile.X - tlTile.X) / 2);
            double tileCentreY = tlTile.Y + ((brTile.Y - tlTile.Y) / 2);

#if DEBUG_PROCESS
            IPath centrePath = new PathBuilder()
            .AddLine((float)tileCentreX - WebTileMinX + 5, (float)tileCentreY - WebTileMinY, (float)tileCentreX - WebTileMinX - 5, (float)tileCentreY - WebTileMinY)
            .AddLine((float)tileCentreX - WebTileMinX, (float)tileCentreY - WebTileMinY + 5, (float)tileCentreX - WebTileMinX, (float)tileCentreY - WebTileMinY - 5)
            .Build();
            image.Mutate(img => img.Draw(Color.DarkGreen, 1.0f, centrePath));

            image.SaveAsBmp("working/" + Id + ".stage0" + ".bmp", new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
#endif

            //Pad the image to make the tile centre the centre of the image
            int tlImageAfterX = tlImageX;
            int tlImageAfterY = tlImageY;

            //If area left of tile is bigger than area right of tile
            if (tlTile.X - tlImageX > tlImageX + image.Width - brTile.X)
            {
                image.Mutate(img => img.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.Left,
                    Size = new Size((int)((tlTile.X - tlImageX) * 2 + (brTile.X - tlTile.X)), image.Height)
                }));
            }
            //If area right of tile is bigger than area left of tile
            else
            {
                tlImageAfterX -= (int)((tlImageX + image.Width - brTile.X) - (tlTile.X - tlImageX));
                image.Mutate(img => img.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.Right,
                    Size = new Size((int)((tlImageX + image.Width - brTile.X) * 2 + (brTile.X - tlTile.X)), image.Height)
                }));
            }

            //If area above tile is bigger than area below tile
            if (tlTile.Y - tlImageY > tlImageY + image.Height - brTile.Y)
            {
                image.Mutate(img => img.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.Top,
                    Size = new Size(image.Width, (int)((tlTile.Y - tlImageY) * 2 + (brTile.Y - tlTile.Y)))
                }));
            }
            //If area below tile is bigger than area above tile
            else
            {
                tlImageAfterY -= (int)((tlImageY + image.Height - brTile.Y) - (tlTile.Y - tlImageY));
                image.Mutate(img => img.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.Bottom,
                    Size = new Size(image.Width, (int)((tlImageY + image.Height - brTile.Y) * 2 + (brTile.Y - tlTile.Y)))
                }));
            }

#if DEBUG_PROCESS
            image.SaveAsBmp("working/" + Id + ".stage1" + ".bmp", new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
#endif

            //Rotate image by the error
            double rotationError = GetRotationError(tlTile, brTile) * (180 / Math.PI);
            image.Mutate(img => img.Rotate((float)(-rotationError)));

            //Crop down to the correct location
            Rectangle cropRect = new Rectangle(
                (int)(((double)image.Width / 2) - (tileWidth / 2)),
                (int)(((double)image.Height / 2) - (tileHeight / 2)),
                (int)tileWidth,
                (int)tileHeight
                );

#if DEBUG_PROCESS
            image.Mutate(img => img.Draw(Color.Red, 1.0f, cropRect));
            image.SaveAsBmp("working/" + Id + ".stage2" + ".bmp", new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
#endif

            image.Mutate(img => img.Crop(cropRect));

            //Resize to 64x64
            image.Mutate(img => img.Resize(new ResizeOptions() { Size = new Size(64, 64), Mode = ResizeMode.Stretch }));

            //Replace pixels off the border with transparent
            image.ProcessPixelRows(processor =>
            {
                Rgba32 transparent = Color.Transparent;
                double metersWidth = BottomRight.Easting - TopLeft.Easting;
                double metersHeight = TopLeft.Northing - BottomRight.Northing;
                for (int y = 0; y < 64; y++)
                {
                    Span<Rgba32> rowSpan = processor.GetRowSpan(y);
                    for (int x = 0; x < 64; x++)
                    {
                        if (!area.IsPointInArea(TopLeft.Easting + (metersWidth * ((x + 0.5) / 64.0)), BottomRight.Northing + (metersHeight * ((63.5 - y) / 64.0))))
                        {
                            rowSpan[x] = transparent;
                        }
                    }
                }
            });

            //Make background white
            image.Mutate(img => img.BackgroundColor(Color.White));

            byte[] outputData = new byte[64 * 64 * 4];
            image.CopyPixelDataTo(new Span<byte>(outputData));
            return outputData;
        }

        public async Task<byte[]> ProcessImageHW(Image<Rgba32>?[] images, OpenGLManager glManager, int gpuTilesWidth, int gpuTilesHeight)
        {
            byte[][] convertedImages = new byte[images.Length][];
            for (int i = 0; i < images.Length; i++)
            {
                convertedImages[i] = new byte[256 * 256 * 4];
                //If we don't copy anything it'll just be left as 0s - aka transparent pixels
                Image<Rgba32>? thisImage = images[i];
                if (thisImage != null)
                {
                    thisImage.CopyPixelDataTo(convertedImages[i]);
                    thisImage.Dispose();
                }
            }
            //These are the pixel locations of the top left of the input image
            int tlImageX = WebTileMinX * 256;
            int tlImageY = WebTileMinY * 256;
            //These are the pixel locations of the top left and bottom right corners of the output image
            Coordinate tlTile = WebTile.GetPixelsFromLonLatExact(LatlongTopLeft, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate brTile = WebTile.GetPixelsFromLonLatExact(LatlongBottomRight, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate trTile = WebTile.GetPixelsFromLonLatExact(LatlongTopRight, WebTile.GetMaxZoomFromScale(MapScale));
            Coordinate blTile = WebTile.GetPixelsFromLonLatExact(LatlongBottomLeft, WebTile.GetMaxZoomFromScale(MapScale));
            double tileWidth = Math.Sqrt(Math.Pow(trTile.X - tlTile.X, 2) + Math.Pow(trTile.Y - tlTile.Y, 2));
            double tileHeight = Math.Sqrt(Math.Pow(blTile.X - tlTile.X, 2) + Math.Pow(blTile.Y - tlTile.Y, 2));
            //Rotation error
            double rotationError = GetRotationError(tlTile, brTile);

            //Stencil stuff
            double metersWidth = BottomRight.Easting - TopLeft.Easting;
            double metersHeight = TopLeft.Northing - BottomRight.Northing;

            //Send tile to GPU
            byte[] processedData = await glManager.ProcessTileAsync(convertedImages, WebTileWidth * 256, WebTileHeight * 256, gpuTilesWidth * 256, gpuTilesHeight * 256, tlTile.X - tlImageX + (tileWidth / 2), tlTile.Y - tlImageY + (tileHeight / 2), tileWidth, tileHeight, -rotationError, (metersWidth / 2) + TopLeft.Easting, (metersHeight / 2) + BottomRight.Northing, metersWidth / 1000, metersHeight / 1000);

            return processedData;
        }

        /// <summary>
        /// Converts image to palette and compress it using the best compression method
        /// </summary>
        /// <param name="palette">The colour palette to use</param>
        /// <returns>The compressed byte data</returns>
        public byte[] ConvertToPaletteAndCompress(byte[] imageData, IQuantizer<Rgba32> quantizer)
        {
            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(imageData, 64, 64);

#if DEBUG_PROCESS
            image.SaveAsPng("working/" + TopLeft.Northing + "_" + TopLeft.Easting + ".processed.png");
#endif
            byte[] tileData = ConvertToPalette(image, quantizer);
#if DEBUG_PROCESS
            image.SaveAsPng("working/" + TopLeft.Northing + "_" + TopLeft.Easting + ".palette.png");
#endif
            image.Dispose();

            tileData = InterlaceRows(tileData);

            tileData = CompressTile(tileData);

            return tileData;
        }

        /// <summary>
        /// Create an array of palette indices from the image
        /// </summary>
        /// <param name="tile">The image to convert</param>
        /// <param name="palette">The palette to use</param>
        /// <returns>A list of palette indices representing the image data</returns>
        private byte[] ConvertToPalette(Image<Rgba32> tile, IQuantizer<Rgba32> quantizer)
        {
            byte[] paletteIndexes = new byte[4096];

            IndexedImageFrame<Rgba32> indexes = quantizer.QuantizeFrame(tile.Frames[0], tile.Frames[0].Bounds());
            for (int i = 0; i < indexes.Height; i++)
            {
                ReadOnlySpan<byte> row = indexes.DangerousGetRowSpan(i);
                row.CopyTo(new Span<byte>(paletteIndexes).Slice(i * indexes.Width, indexes.Width));
            }

            return paletteIndexes;
        }

        /// <summary>
        /// Reverse the order of the bits in a byte
        /// </summary>
        /// <param name="inByte">The byte to reverse</param>
        /// <returns>The reversed byte</returns>
        public static byte Reverse6Bits(byte inByte)
        {
            byte result = 0x00;

            //Iterate over every bit, from left to right
            for (byte mask = 0x20; Convert.ToInt32(mask) > 0; mask >>= 1)
            {
                //Shift all the bits in the result to the right
                result = (byte)(result >> 1);

                //If there is a 1 on the current bit, tempbyte is 1
                var tempbyte = (byte)(inByte & mask);

                //If tempbyte is 1, set the leftmost bit of result to 1. Otherwise, leave it at 0
                if (tempbyte != 0x00)
                {
                    //Set the leftmost bit of the result to a 1
                    result = (byte)(result | 0x80);
                }
            }

            //Shift the result 2 places to the right because we're doing 6 bits not 8
            result >>= 2;

            return (result);
        }

        /// <summary>
        /// Interlace the rows
        /// </summary>
        /// <param name="tile">The image palette indices to convert</param>
        /// <returns>The converted image palette indices</returns>
        private byte[] InterlaceRows(byte[] tile)
        {
            byte[] convertedTile = new byte[4096];

            //Iterate over every row
            for (byte sourceRow = 0; sourceRow < 64; sourceRow++)
            {
                //To get the destination row, reverse the binary of the source row
                byte destinationRow = Reverse6Bits(sourceRow);

                //Insert source row bytes into destination row
                Array.Copy(tile, (int)sourceRow * 64, convertedTile, (int)destinationRow * 64, 64);
            }

            return convertedTile;
        }

        /// <summary>
        /// Compresses the tile data using the best compression type
        /// </summary>
        /// <param name="tile">The uncompressed tile data</param>
        /// <returns></returns>
        private byte[] CompressTile(byte[] tile)
        {
            byte[] runLengthCompressed = RunLength.Compress(tile);
            log.LogTrace("Run length compression is {size} bytes", runLengthCompressed.Length);

            byte[] pixelPackingCompressed = PixelPacking.Compress(tile);
            log.LogTrace("Pixel packing compression is {size} bytes", pixelPackingCompressed.Length);

            byte[] huffmanCodingCompressed = HuffmanCoding.Compress(tile);
            log.LogTrace("Huffman coding compression is {size} bytes", huffmanCodingCompressed.Length);

            //Return whichever one is smaller
            if (runLengthCompressed.Length < pixelPackingCompressed.Length && runLengthCompressed.Length < huffmanCodingCompressed.Length)
            {
                log.LogDebug("Using run length compression");
                return runLengthCompressed;
            }
            else if (pixelPackingCompressed.Length < runLengthCompressed.Length && pixelPackingCompressed.Length < huffmanCodingCompressed.Length)
            {
                log.LogDebug("Using pixel packing compression");
                return pixelPackingCompressed;
            }
            else
            {
                log.LogDebug("Using huffman coding compression");
                return huffmanCodingCompressed;
            }
        }
    }
}
