using System;
using System.Collections.Generic;
using System.Linq;
using OsMapDownloader.Qct.InterpolationMatrix;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace OsMapDownloader.Qct.WebDownloader
{
    public class PaletteCreator
    {
        private Scale _mapScale;
        private OctreeQuantizer<Rgb24> _quantizer;

        public PaletteCreator(Scale mapScale)
        {
            _mapScale = mapScale;
            _quantizer = new OctreeQuantizer<Rgb24>(new Configuration(), new QuantizerOptions() { MaxColors = 128 - InterpolationMatrixCreator.GetMapColours(_mapScale).Count, Dither = null });
        }

        public Rgb24[] GetPalette()
        {
            Rgb24[] palette = _quantizer.Palette.ToArray();
            return InterpolationMatrixCreator.GetMapColours(_mapScale).Concat(palette).ToArray();
        }

        public void AddImage(byte[] imageData)
        {
            //using Image<Rgb24> image = Image.Load<Rgb24>(imageData);
            using Image<Rgb24> image = Image.Load<Rgb24>(imageData);
            _quantizer.AddPaletteColors(new Buffer2DRegion<Rgb24>(image.Frames.RootFrame.PixelBuffer));
        }
    }
}
