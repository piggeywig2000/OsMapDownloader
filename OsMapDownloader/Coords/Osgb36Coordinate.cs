using System.Text.Json.Serialization;
using GeoUK;
using GeoUK.Coordinates;
using GeoUK.Ellipsoids;
using GeoUK.Projections;

namespace OsMapDownloader.Coords
{
    public class Osgb36Coordinate : Vector2<Osgb36Coordinate>
    {
        public double Easting { get => x; }
        public double Northing { get => y; }

        public Osgb36Coordinate() : base() { }
        /// <summary>
        /// Create a new OSGB36 coordinate with the specified easting and northing
        /// </summary>
        /// <param name="easting">The easting of the coordinate</param>
        /// <param name="northing">The northing of the coordinate</param>
        [JsonConstructor]
        public Osgb36Coordinate(double easting, double northing) : base(easting, northing) { }

        public override string ToString()
        {
            return $"{Easting}E {Northing}N";
        }

        /// <summary>
        /// Convert to WGS84 latitude/longitude to an accuracy of around 10cm
        /// </summary>
        /// <returns>The WGS84 coordinates</returns>
        public Wgs84Coordinate ToWgs84Accurate()
        {
            LatitudeLongitude latLong;
            try
            {
                latLong = GeoUK.OSTN.Transform.OsgbToEtrs89(new Osgb36(Easting, Northing));
            }
            catch (KeyNotFoundException)
            {
                Cartesian cartesian = GeoUK.Convert.ToCartesian(new Airy1830(), new BritishNationalGrid(), new EastingNorthing(Easting, Northing));
                cartesian = Transform.Osgb36ToEtrs89(cartesian);
                latLong = GeoUK.Convert.ToLatitudeLongitude(new Wgs84(), cartesian);
            }
            return new Wgs84Coordinate(latLong.Longitude, latLong.Latitude);
        }

        public string GetBNGSquare() => Osgb36.GetBngSquare(Easting, Northing);
    }
}
