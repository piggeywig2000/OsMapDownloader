using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Border;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Qed
{
    internal class Tile
    {
        public Osgb36Coordinate TopLeft { get; }

        public Tile(Osgb36Coordinate topLeftCorner)
        {
            TopLeft = topLeftCorner;
        }

        public async Task<int[]> GetData(TerrainDataReader dataReader, uint pointsPerTile, double metersBetweenPoints, CancellationToken cancellationToken = default(CancellationToken))
        {
            int[] data = new int[pointsPerTile * pointsPerTile];

            for (int y = 0; y < pointsPerTile; y++)
            {
                for (int x = 0; x < pointsPerTile; x++)
                {
                    Osgb36Coordinate pointCoord = new Osgb36Coordinate(TopLeft.Easting + (x * metersBetweenPoints), TopLeft.Northing - (y * metersBetweenPoints));
                    data[(y * pointsPerTile) + x] = (int)Math.Round(await dataReader.GetHeightAtPoint(pointCoord, cancellationToken));
                }
            }

            return data;
        }
    }
}
