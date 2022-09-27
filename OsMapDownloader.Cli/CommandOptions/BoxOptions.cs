using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace OsMapDownloader.Cli.CommandOptions
{
    [Verb("box", HelpText = "Create a rectangular map by defining the top left corner and bottom right corner of the map")]
    internal class BoxOptions : CommonOptions
    {
        [Value(0, HelpText = "The latitude of the top left corner of the bounding box", Required = true, MetaName = "TopLeftLatitude")]
        public double TopLeftLatitude { get; set; }

        [Value(1, HelpText = "The longitude of the top left corner of the bounding box", Required = true, MetaName = "TopLeftLongitude")]
        public double TopLeftLongitude { get; set; }

        [Value(2, HelpText = "The latitude of the bottom right corner of the bounding box", Required = true, MetaName = "BottomRightLatitude")]
        public double BottomRightLatitude { get; set; }

        [Value(3, HelpText = "The longitude of the bottom right corner of the bounding box", Required = true, MetaName = "BottomRightLongitude")]
        public double BottomRightLongitude { get; set; }
    }
}
