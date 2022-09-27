using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Gui.Config
{
    public class Settings
    {
        public int PolynomialSampleSize { get; set; } = 2500;
        public string? Token { get; set; } = null;
        public bool UseHardwareAcceleration { get; set; } = true;
        public bool KeepTiles { get; set; } = false;
    }
}
