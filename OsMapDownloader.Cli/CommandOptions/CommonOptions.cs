using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace OsMapDownloader.Cli.CommandOptions
{
    internal class CommonOptions
    {
        [Value(4, HelpText = "The path to where the file should be saved. Can be a relative or absolute path", Required = true, MetaName = "FilePath")]
        public string DestinationPath { get; set; } = "";

        [Option('s', "silent", Default = false, HelpText = "Do not write anything to the console")]
        public bool Silent { get; set; }

        [Option('q', "quiet", Default = false, HelpText = "Only write errors to the console")]
        public bool Quiet { get; set; }

        [Option('d', "debug", Default = false, HelpText = "Write some more information to the console. Useful for debugging")]
        public bool Debug { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "Write a lot of information to the console. Useful for debugging")]
        public bool Verbose { get; set; }

        [Option('o', "overwrite", Default = false, HelpText = "If the file exists, overwrite it")]
        public bool Overwrite { get; set; }

        private string? _stringScale;
        [Option("scale", Default = "1:25000", HelpText = "The scale of the map to download. Either 1:25000, 1:50000, 1:250000, or 1:1000000")]
        public string? StringScale
        {
            get => _stringScale;
            set
            {
                _stringScale = value;
                Scale = value switch
                {
                    "explorer" or "1:25000" or "1:25k" or "25000" or "25" or "25k" => Scale.Explorer,
                    "landranger" or "1:50000" or "1:50k" or "50000" or "50" or "50k" => Scale.Landranger,
                    "road" or "1:250000" or "1:250k" or "250000" or "250" or "250k" => Scale.Road,
                    "miniscale" or "1:1000000" or "1:1000k" or "1:1m" or "1000000" or "1000" or "1000k" or "1m" => Scale.MiniScale,
                    _ => throw new ArgumentException("Invalid scale"),
                };
            }
        }
        public Scale Scale { get; private set; }

        [Option("polynomial-sample-size", Default = 2500, HelpText = "The number of rows and columns in the grid of samples taken when calculating the polynomial coefficients for GPS coordinate transformations.\nIncrease this for a higher GPS accuracy, decrease this for lower memory usage and a faster processing time.")]
        public int PolynomialSampleSize { get; set; }

        [Option("token", Default = null, HelpText = "The token to use when downloading tiles. By default the program will try to fetch this automatically, but you can manually specify it with this option if that doesn't work")]
        public string? Token { get; set; }

        [Option("disable-hw-accel", Default = false, HelpText = "Tiles are processed on the CPU instead of the GPU. It will reduce processing speed, so keep this disabled unless you're having issues")]
        public bool DisableHardwareAccel { get; set; }

        [Option("keep-tiles", Default = false, HelpText = "Don't delete the downloaded tiles after completion")]
        public bool KeepTiles { get; set; }

        [Option("long-name", HelpText = "A longer version of the map's name")]
        public string? LongName { get; set; }

        [Option("name", HelpText = "The map's name")]
        public string? Name { get; set; }

        [Option("identifier", HelpText = "Metadata thing, but I have no idea what it is. Seems to be used to identify a specific place, such as an airport")]
        public string? Identifier { get; set; }

        [Option("edition", HelpText = "The map edition. Usually a year")]
        public string? Edition { get; set; }

        [Option("revision", HelpText = "The map revision. Usually a number; 1 if it's the first revision")]
        public string? Revision { get; set; }

        [Option("keywords", HelpText = "Metadata thing. I have never seen this used")]
        public string? Keywords { get; set; }

        [Option("copyright", HelpText = "Copyright information")]
        public string? Copyright { get; set; }

        private string? _metadataScale;
        [Option("custom-scale", Default = null, HelpText = "The map scale. Defaults to a value based on the scale")]
        public string? MetadataScale
        {
            get => string.IsNullOrEmpty(_metadataScale) ? $"1:{(int)Scale:N0}" : _metadataScale;
            set => _metadataScale = value;
        }

        [Option("datum", Default = "WGS84", HelpText = "The datum used")]
        public string? Datum { get; set; }

        [Option("depths", Default = "Meters", HelpText = "The depth units")]
        public string? Depths { get; set; }

        [Option("heights", Default = "Meters", HelpText = "The height units")]
        public string? Heights { get; set; }

        [Option("projection", Default = "UTM", HelpText = "The projection used")]
        public string? Projection { get; set; }

        private string? _mapType;
        [Option("type", Default = null, HelpText = "The map type. Defaults to Land for Explorer and Landranger scales, and Road for Road and MiniScale scales")]
        public string? MapType
        {
            get => string.IsNullOrEmpty(_mapType) ? ((int)Scale <= 50000 ? "Land" : "Road") : _mapType;
            set => _mapType = value;
        }
    }
}
