using System.Text.Json.Serialization;
using GeoUK;
using GeoUK.Coordinates;
using GeoUK.Ellipsoids;
using GeoUK.Projections;

namespace OsMapDownloader.Coords
{
    public class Wgs84Coordinate : Vector2<Wgs84Coordinate>
    {
        public double Longitude { get => x; }
        public double Latitude { get => y; }

        public Wgs84Coordinate() : base() { }
        /// <summary>
        /// Create a new WGS84 coordinate with the specified longitude and latitude
        /// </summary>
        /// <param name="longitude">The longitude of the coordinate</param>
        /// <param name="latitude">The latitude of the coordinate</param>
        [JsonConstructor]
        public Wgs84Coordinate(double longitude, double latitude) : base(longitude, latitude) { }

        public override string ToString()
        {
            return $"{Latitude}°N {Longitude}°E";
        }

        /// <summary>
        /// Convert to OSGB36 eastings/northings to an accuracy of around 10cm
        /// </summary>
        /// <returns>The OSGB36 coordinates</returns>
        public Osgb36Coordinate ToOsgb36Accurate()
        {
            LatitudeLongitude latLon = new LatitudeLongitude(Latitude, Longitude);
            Osgb36Coordinate converted;
            try
            {
                Osgb36 eastNorth = GeoUK.OSTN.Transform.Etrs89ToOsgb(latLon);
                converted = new Osgb36Coordinate(eastNorth.Easting, eastNorth.Northing);
            }
            catch (KeyNotFoundException)
            {
                Cartesian cartesian = GeoUK.Convert.ToCartesian(new Wgs84(), latLon);
                cartesian = Transform.Etrs89ToOsgb36(cartesian);
                EastingNorthing eastNorth = GeoUK.Convert.ToEastingNorthing(new Airy1830(), new BritishNationalGrid(), cartesian);
                converted = new Osgb36Coordinate(eastNorth.Easting, eastNorth.Northing);
            }
            return converted;
        }
    }
}
