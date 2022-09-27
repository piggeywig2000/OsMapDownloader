using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace OsMapDownloader.Cli.CommandOptions
{
    [Verb("points", HelpText = "Create a map of any shape by defining the border of a map as a series of points, which are joined together clockwise. The file should contain a list of points separated by commas")]
    internal class PointsOptions : CommonOptions
    {
        [Value(0, HelpText = "The path to the file containing the border points. Can be a relative or absolute path", Required = true, MetaName = "BorderPointsPath")]
        public string PointsPath { get; set; } = "";
    }
}
