using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OsMapDownloader.Qct.InterpolationMatrix
{
    public class InterpolationMatrixCreator
    {
        private readonly Rgb24[] _palette;
        private readonly int MAX_PRIORITY;
        private readonly Scale _mapScale;
        private static readonly Dictionary<Scale, List<KeyValuePair<Rgb24, int>>> _colourPriority = new Dictionary<Scale, List<KeyValuePair<Rgb24, int>>>()
        {
            { Scale.Explorer, new List<KeyValuePair<Rgb24, int>>()
            {
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 0, 0), 0), //Black
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 255, 255), 0), //Background white

                new KeyValuePair<Rgb24, int>(new Rgb24(246, 151, 122), 0), //Contour definition
                new KeyValuePair<Rgb24, int>(new Rgb24(243, 135, 86), 0), //Contour
                new KeyValuePair<Rgb24, int>(new Rgb24(207, 129, 85), 0), //Contour forest

                new KeyValuePair<Rgb24, int>(new Rgb24(254, 218, 174), 1), //Building
                new KeyValuePair<Rgb24, int>(new Rgb24(219, 237, 201), 1), //Forest
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 206, 158), 1), //Open access border
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 253, 233), 1), //Open access land
                new KeyValuePair<Rgb24, int>(new Rgb24(223, 228, 129), 1), //Open access forest
                new KeyValuePair<Rgb24, int>(new Rgb24(254, 219, 171), 1), //Open access sand
                new KeyValuePair<Rgb24, int>(new Rgb24(244, 153, 194), 1), //Coastal margin border
                new KeyValuePair<Rgb24, int>(new Rgb24(252, 210, 189), 1), //Coastal margin sand
                new KeyValuePair<Rgb24, int>(new Rgb24(252, 233, 241), 1), //Coastal margin rock
                new KeyValuePair<Rgb24, int>(new Rgb24(216, 208, 194), 1), //Mud
                new KeyValuePair<Rgb24, int>(new Rgb24(253, 221, 195), 1), //Sand
                new KeyValuePair<Rgb24, int>(new Rgb24(238, 29, 35), 1), //Danger
                new KeyValuePair<Rgb24, int>(new Rgb24(244, 153, 194), 1), //National park boundary
                new KeyValuePair<Rgb24, int>(new Rgb24(249, 204, 223), 1), //National park boundary light

                new KeyValuePair<Rgb24, int>(new Rgb24(212, 239, 251), 2), //Water
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 133, 66), 2), //Footpath
                new KeyValuePair<Rgb24, int>(new Rgb24(232, 107, 46), 2), //Permissive path

                new KeyValuePair<Rgb24, int>(new Rgb24(254, 230, 0), 3), //Smol road
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 168, 21), 3), //Road
                new KeyValuePair<Rgb24, int>(new Rgb24(234, 109, 47), 3), //Secondary road

                new KeyValuePair<Rgb24, int>(new Rgb24(87, 187, 234), 4), //Blue grid lines
                new KeyValuePair<Rgb24, int>(new Rgb24(219, 42, 128), 4), //Main road
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 152, 224), 4), //Motorway
            } },
            { Scale.Landranger, new List<KeyValuePair<Rgb24, int>>()
            {
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 0, 0), 0), //Black
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 255, 255), 0), //Background white

                new KeyValuePair<Rgb24, int>(new Rgb24(245, 147, 119), 0), //Contour definition
                new KeyValuePair<Rgb24, int>(new Rgb24(243, 136, 115), 0), //Contour
                new KeyValuePair<Rgb24, int>(new Rgb24(206, 133, 114), 0), //Contour forest

                new KeyValuePair<Rgb24, int>(new Rgb24(253, 220, 194), 1), //Building
                new KeyValuePair<Rgb24, int>(new Rgb24(221, 233, 171), 1), //Forest
                new KeyValuePair<Rgb24, int>(new Rgb24(204, 198, 228), 1), //Open access border
                new KeyValuePair<Rgb24, int>(new Rgb24(207, 189, 160), 1), //Open access border forest
                new KeyValuePair<Rgb24, int>(new Rgb24(152, 113, 168), 1), //Access land label
                new KeyValuePair<Rgb24, int>(new Rgb24(221, 224, 196), 1), //Mud
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 237, 194), 1), //Sand

                new KeyValuePair<Rgb24, int>(new Rgb24(212, 239, 251), 2), //Water
                //We have no footpath because it's the same colour as the main road

                new KeyValuePair<Rgb24, int>(new Rgb24(254, 230, 0), 3), //Road
                new KeyValuePair<Rgb24, int>(new Rgb24(247, 141, 37), 3), //Secondary road

                //We have no grid lines because it's the same colour as the motorway
                new KeyValuePair<Rgb24, int>(new Rgb24(217, 42, 127), 4), //Main road
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 133, 66), 4), //Primary road
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 153, 226), 4), //Motorway
            } },
            { Scale.Road, new List<KeyValuePair<Rgb24, int>>()
            {
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 0, 0), 0), //Black
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 255, 255), 0), //Background white

                new KeyValuePair<Rgb24, int>(new Rgb24(244, 153, 194), 0), //Contour definition
                new KeyValuePair<Rgb24, int>(new Rgb24(250, 220, 220), 0), //Contour
                new KeyValuePair<Rgb24, int>(new Rgb24(230, 200, 190), 0), //Contour forest
                new KeyValuePair<Rgb24, int>(new Rgb24(245, 180, 180), 0), //Contour hilly
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 254, 244), 0), //Relief 200-600
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 253, 233), 0), //Relief 600-1000
                new KeyValuePair<Rgb24, int>(new Rgb24(254, 239, 208), 0), //Relief 1000-1400
                new KeyValuePair<Rgb24, int>(new Rgb24(254, 224, 182), 0), //Relief 1400-2000
                new KeyValuePair<Rgb24, int>(new Rgb24(253, 211, 159), 0), //Relief 2000-3000
                new KeyValuePair<Rgb24, int>(new Rgb24(252, 200, 151), 0), //Relief 3000+

                new KeyValuePair<Rgb24, int>(new Rgb24(184, 184, 184), 1), //Buildings
                new KeyValuePair<Rgb24, int>(new Rgb24(216, 232, 174), 1), //Wood
                new KeyValuePair<Rgb24, int>(new Rgb24(248, 248, 8), 1), //National part boundary
                new KeyValuePair<Rgb24, int>(new Rgb24(200, 216, 216), 1), //Foreshore
                new KeyValuePair<Rgb24, int>(new Rgb24(248, 247, 151), 1), //Sand

                new KeyValuePair<Rgb24, int>(new Rgb24(228, 240, 254), 2), //Water
                //We have no national trail because it's the same colour as the primary road

                new KeyValuePair<Rgb24, int>(new Rgb24(255, 255, 90), 3), //Road
                new KeyValuePair<Rgb24, int>(new Rgb24(250, 200, 105), 3), //Secondary road
                new KeyValuePair<Rgb24, int>(new Rgb24(60, 45, 45), 3), //Railway

                new KeyValuePair<Rgb24, int>(new Rgb24(55, 170, 230), 4), //Grid lines
                new KeyValuePair<Rgb24, int>(new Rgb24(220, 55, 149), 4), //Main road
                new KeyValuePair<Rgb24, int>(new Rgb24(8, 152, 72), 4), //Primary road
                new KeyValuePair<Rgb24, int>(new Rgb24(248, 248, 8), 4), //Primary road dual carridgeway
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 153, 226), 4), //Motorway
            } },
            { Scale.MiniScale, new List<KeyValuePair<Rgb24, int>>()
            {
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 0, 0), 0), //Black
                new KeyValuePair<Rgb24, int>(new Rgb24(255, 255, 255), 0), //Background white

                new KeyValuePair<Rgb24, int>(new Rgb24(241, 225, 237), 0), //Urban area
                new KeyValuePair<Rgb24, int>(new Rgb24(254, 232, 207), 0), //National park
                new KeyValuePair<Rgb24, int>(new Rgb24(217, 237, 226), 0), //Area of outstanding natural beauty
                new KeyValuePair<Rgb24, int>(new Rgb24(190, 226, 211), 0), //Forest

                new KeyValuePair<Rgb24, int>(new Rgb24(255, 241, 1), 1), //Town marker
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 125, 173), 1), //Ship

                new KeyValuePair<Rgb24, int>(new Rgb24(200, 200, 200), 2), //County boundary
                new KeyValuePair<Rgb24, int>(new Rgb24(199, 234, 252), 2), //Water

                new KeyValuePair<Rgb24, int>(new Rgb24(245, 163, 199), 3), //Road
                new KeyValuePair<Rgb24, int>(new Rgb24(238, 32, 155), 3), //Road label
                new KeyValuePair<Rgb24, int>(new Rgb24(110, 110, 110), 3), //Railway

                new KeyValuePair<Rgb24, int>(new Rgb24(176, 154, 201), 4), //National boundary
                new KeyValuePair<Rgb24, int>(new Rgb24(115, 198, 157), 4), //Primary road
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 166, 82), 4), //Primary road label
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 189, 242), 4), //Motorway
                new KeyValuePair<Rgb24, int>(new Rgb24(0, 174, 239), 4), //Motorway label
            } }
        };

        public InterpolationMatrixCreator(Color[] palette, Scale mapScale)
        {
            _palette = palette.Select(color => color.ToPixel<Rgb24>()).ToArray();
            MAX_PRIORITY = _colourPriority[mapScale].Max(kvp => kvp.Value);
            _mapScale = mapScale;
        }

        public static ReadOnlyCollection<Rgb24> GetMapColours(Scale mapScale)
        {
            return new ReadOnlyCollection<Rgb24>(_colourPriority[mapScale].Select(kvp => kvp.Key).ToList());
        }

        public byte[] GetInterpolationMatrix()
        {
            byte[] matrix = new byte[16384];
            for (byte y = 0; y < _palette.Length; y++)
            {
                for (byte x = 0; x < _palette.Length; x++)
                {
                    matrix[y * 128 + x] = MergeColours(x, y);
                }
            }
            return matrix;
        }

        private byte MergeColours(byte aIndex, byte bIndex)
        {
            Rgb24 a = _palette[aIndex];
            Rgb24 b = _palette[bIndex];
            double aPrio = GetPriority(aIndex);
            double bPrio = GetPriority(bIndex);
            double prioDiff = (aPrio - bPrio) / (MAX_PRIORITY * 2); //Between -0.5 and 0.5. Positive = a higher, Negative = b higher
            Rgb24 middle = new Rgb24((byte)(a.R - 0.5 * (a.R - b.R)), (byte)(a.G - 0.5 * (a.G - b.G)), (byte)(a.B - 0.5 * (a.B - b.B)));
            Rgb24 result = new Rgb24((byte)(middle.R + prioDiff * (a.R - b.R)), (byte)(middle.G + prioDiff * (a.G - b.G)), (byte)(middle.B + prioDiff * (a.B - b.B)));

            //Now find the closest matching colour in our palette
            double shortestDistance = double.MaxValue;
            byte resultIndex = 0;
            for (byte i = 0; i < _palette.Length; i++)
            {
                double distance = ColourDistance(result, _palette[i]);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    resultIndex = i;
                }
            }
            return resultIndex;
        }

        private double GetPriority(byte index)
        {
            Rgb24 target = _palette[index];
            double shortestDistance = double.MaxValue;
            int priority = 0;
            foreach (KeyValuePair<Rgb24, int> kvp in _colourPriority[_mapScale])
            {
                double distance = ColourDistance(target, kvp.Key);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    priority = kvp.Value;
                }
            }
            return priority;
        }

        private double ColourDistance(Rgb24 a, Rgb24 b)
        {
            return Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2);
        }
    }
}
