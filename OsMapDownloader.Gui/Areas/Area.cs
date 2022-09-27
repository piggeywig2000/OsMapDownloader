using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Gui.Areas
{
    public class Area
    {
        public event EventHandler? OnRename;

        public Area(string name)
        {
            Name = name;
            Points = new List<Wgs84Coordinate>();
        }

        [JsonConstructor]
        public Area(string name, List<Wgs84Coordinate> points)
        {
            Name = name;
            Points = points;
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnRename?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public List<Wgs84Coordinate> Points { get; }

        public override string ToString() => Name;
    }
}
