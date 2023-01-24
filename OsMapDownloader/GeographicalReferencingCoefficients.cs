using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Qct;
using Serilog;

namespace OsMapDownloader
{
    public struct GeographicalReferencingCoefficients
    {
        public GeographicalReferencingCoefficients(
            double eas, double easY, double easX, double easYY, double easXY, double easXX, double easYYY, double easYYX, double easYXX, double easXXX,
            double nor, double norY, double norX, double norYY, double norXY, double norXX, double norYYY, double norYYX, double norYXX, double norXXX,
            double lat, double latX, double latY, double latXX, double latXY, double latYY, double latXXX, double latXXY, double latXYY, double latYYY,
            double lon, double lonX, double lonY, double lonXX, double lonXY, double lonYY, double lonXXX, double lonXXY, double lonXYY, double lonYYY)
        {
            Eas = eas;
            EasY = easY;
            EasX = easX;
            EasYY = easYY;
            EasXY = easXY;
            EasXX = easXX;
            EasYYY = easYYY;
            EasYYX = easYYX;
            EasYXX = easYXX;
            EasXXX = easXXX;

            Nor = nor;
            NorY = norY;
            NorX = norX;
            NorYY = norYY;
            NorXY = norXY;
            NorXX = norXX;
            NorYYY = norYYY;
            NorYYX = norYYX;
            NorYXX = norYXX;
            NorXXX = norXXX;

            Lat = lat;
            LatX = latX;
            LatY = latY;
            LatXX = latXX;
            LatXY = latXY;
            LatYY = latYY;
            LatXXX = latXXX;
            LatXXY = latXXY;
            LatXYY = latXYY;
            LatYYY = latYYY;

            Lon = lon;
            LonX = lonX;
            LonY = lonY;
            LonXX = lonXX;
            LonXY = lonXY;
            LonYY = lonYY;
            LonXXX = lonXXX;
            LonXXY = lonXXY;
            LonXYY = lonXYY;
            LonYYY = lonYYY;
        }

        public double Eas { get; set; }
        public double EasY { get; set; }
        public double EasX { get; set; }
        public double EasYY { get; set; }
        public double EasXY { get; set; }
        public double EasXX { get; set; }
        public double EasYYY { get; set; }
        public double EasYYX { get; set; }
        public double EasYXX { get; set; }
        public double EasXXX { get; set; }

        public double Nor { get; set; }
        public double NorY { get; set; }
        public double NorX { get; set; }
        public double NorYY { get; set; }
        public double NorXY { get; set; }
        public double NorXX { get; set; }
        public double NorYYY { get; set; }
        public double NorYYX { get; set; }
        public double NorYXX { get; set; }
        public double NorXXX { get; set; }

        public double Lat { get; set; }
        public double LatX { get; set; }
        public double LatY { get; set; }
        public double LatXX { get; set; }
        public double LatXY { get; set; }
        public double LatYY { get; set; }
        public double LatXXX { get; set; }
        public double LatXXY { get; set; }
        public double LatXYY { get; set; }
        public double LatYYY { get; set; }

        public double Lon { get; set; }
        public double LonX { get; set; }
        public double LonY { get; set; }
        public double LonXX { get; set; }
        public double LonXY { get; set; }
        public double LonYY { get; set; }
        public double LonXXX { get; set; }
        public double LonXXY { get; set; }
        public double LonXYY { get; set; }
        public double LonYYY { get; set; }

        public async Task Write(FileStream fs, uint location, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Debug("Writing geographical referencing coefficients");
            await fs.WriteDoubleMetadata(location + 0x000, Eas, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x008, EasY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x010, EasX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x018, EasYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x020, EasXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x028, EasXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x030, EasYYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x038, EasYYX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x040, EasYXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x048, EasXXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x050, Nor, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x058, NorY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x060, NorX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x068, NorYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x070, NorXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x078, NorXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x080, NorYYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x088, NorYYX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x090, NorYXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x098, NorXXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0A0, Lat, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0A8, LatX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0B0, LatY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0B8, LatXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0C0, LatXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0C8, LatYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0D0, LatXXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0D8, LatXXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0E0, LatXYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0E8, LatYYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0F0, Lon, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x0F8, LonX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x100, LonY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x108, LonXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x110, LonXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x118, LonYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x120, LonXXX, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x128, LonXXY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x130, LonXYY, cancellationToken);
            await fs.WriteDoubleMetadata(location + 0x138, LonYYY, cancellationToken);
        }
    }
}
