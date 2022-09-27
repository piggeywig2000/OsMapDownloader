using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OsMapDownloader.Coords
{
    public class Coordinate : Vector2<Coordinate>
    {
        public double X { get => x; }
        public double Y { get => y; }

        public Coordinate() : base() { }
        /// <summary>
        /// Create a new coordinate with the specified x and y
        /// </summary>
        /// <param name="x">The x of the coordinate</param>
        /// <param name="y">The y of the coordinate</param>
        [JsonConstructor]
        public Coordinate(double x, double y) : base(x, y) { }

        public override string ToString()
        {
            return $"{X}, {Y}";
        }
    }
}
