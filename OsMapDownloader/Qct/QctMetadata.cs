using System;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Qct
{
    public class QctMetadata
    {
        /// <summary>
        /// The type of file that this is
        /// </summary>
        public QctType FileType { get; set; } = QctType.QuickChartMap;

        /// <summary>
        /// A lengthy version of the name
        /// </summary>
        public string? LongTitle { get; set; } = "";

        /// <summary>
        /// The name
        /// </summary>
        public string? Name { get; set; } = "";

        /// <summary>
        /// ??? Seems to be used for airfield unique codes
        /// </summary>
        public string? Identifier { get; set; } = "";

        /// <summary>
        /// Seems to be the year of the map
        /// </summary>
        public string? Edition { get; set; } = "";

        /// <summary>
        /// The version of this edition. 1 if it's the first version
        /// </summary>
        public string? Revision { get; set; } = "";

        /// <summary>
        /// ????????????
        /// </summary>
        public string? Keywords { get; set; } = "";

        /// <summary>
        /// Copyright information. Eg: 2006 Crown Copyright; Ordnance Survey, Licence Number PU 100034184
        /// </summary>
        public string? Copyright { get; set; } = "";

        /// <summary>
        /// The scale. Eg: 1:25,000
        /// </summary>
        public string? Scale { get; set; } = "";

        /// <summary>
        /// The datum. Eg: WGS84
        /// </summary>
        public string? Datum { get; set; } = "WGS84";

        /// <summary>
        /// The units for depths (sea ig). Eg: Meters
        /// </summary>
        public string? Depths { get; set; } = "Meters";

        /// <summary>
        /// The units for heights. Eg: Meters
        /// </summary>
        public string? Heights { get; set; } = "Meters";

        /// <summary>
        /// The map projection. Eg: UTM
        /// </summary>
        public string? Projection { get; set; } = "UTM";

        /// <summary>
        /// Some flags for some boolean settings
        /// </summary>
        public QctFlags Flags { get; set; } = 0;

        /// <summary>
        /// Whether to write the original file name in the metadata
        /// </summary>
        public bool WriteOriginalFileName { get; set; } = false;

        /// <summary>
        /// Whether to write the original file size in the metadata
        /// </summary>
        public bool WriteOriginalFileSize { get; set; } = false;

        /// <summary>
        /// Whether to write the original file creation time in the metadata
        /// </summary>
        public bool WriteOriginalCreationTime { get; set; } = false;

        /// <summary>
        /// The map type. Eg: Land, Road
        /// </summary>
        public string? MapType { get; set; } = "";

        /// <summary>
        /// Datum shift latitude
        /// </summary>
        public double DatumShiftNorth { get; set; } = 0.0;

        /// <summary>
        /// Datum shift longitude
        /// </summary>
        public double DatumShiftEast { get; set; } = 0.0;

        /// <summary>
        /// An array of coordinates that mark out the corners of the map. Normally 4 for each corner
        /// </summary>
        public Wgs84Coordinate[] MapOutline { get; set; } = new Wgs84Coordinate[0];
    }

    public enum QctType : uint
    {
        QuickChartInformation = 0x1423D5FE,
        QuickChartMap = 0x1423D5FF,
    }

    [Flags]
    public enum QctFlags : uint
    {
        MustHaveOriginalFile = 1,
        AllowCalibration = 2
    }
}
