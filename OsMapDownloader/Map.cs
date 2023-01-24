using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Border;
using OsMapDownloader.Coords;
using OsMapDownloader.Progress;
using OsMapDownloader.Qct;
using Serilog;

namespace OsMapDownloader
{
    public class Map
    {
        public Osgb36Coordinate TopLeft { get; }
        public Osgb36Coordinate BottomRight { get; }
        public Osgb36Coordinate[] Border { get; }
        public MapArea Area { get; }
        public QctMetadata Metadata { get; }
        public Scale Scale { get; }

        public Map(Wgs84Coordinate[] borderPoints, Scale scale, QctMetadata metadata)
        {
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

            Log.Debug("This map has top left corner at {topLeft} and bottom right corner at {bottomRight} when converted to Eastings/Northings", TopLeft, BottomRight);
            Log.Debug("This map has a scale of 1:{scale}", (uint)Scale);

            Area = new MapArea(Border);
        }
    }
}
