using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OsMapDownloader.Coords
{
    public class IntCoordinate : Vector2<IntCoordinate>
    {
        public int X { get => (int)x; }
        public int Y { get => (int)y; }

        public IntCoordinate() : base() { }
        /// <summary>
        /// Create a new coordinate with the specified x and y
        /// </summary>
        /// <param name="x">The x of the coordinate</param>
        /// <param name="y">The y of the coordinate</param>
        [JsonConstructor]
        public IntCoordinate(int x, int y) : base(x, y) { }

        public override string ToString()
        {
            return $"{X}, {Y}";
        }
    }
}
