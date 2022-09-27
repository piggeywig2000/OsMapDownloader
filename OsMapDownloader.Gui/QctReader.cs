using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Coords;
using OsMapDownloader.Qct;

namespace OsMapDownloader.Gui
{
    internal class QctReader
    {
        public string Path { get; }

        public QctReader(string path)
        {
            Path = path;
        }

        public async Task<QctMetadata> ReadMetadata()
        {
            using FileStream fs = new FileStream(Path, FileMode.Open);

            uint version = await fs.ReadIntegerMetadata(0x04);
            if (version != 0x00000002 && version != 0x00000004) throw new IOException("QCT file is the wrong version");

            uint edsPointer = await fs.ReadIntegerMetadata(0x54);
            double[] datumShift = await fs.ReadDoubleArrayMetadata(edsPointer + 0x04, 2);

            uint mapOutlineNumPoints = await fs.ReadIntegerMetadata(0x58);
            double[] mapOutlineArray = await fs.ReadDoubleArrayMetadata(0x5C, (int)mapOutlineNumPoints * 2);
            Wgs84Coordinate[] mapOutline = new Wgs84Coordinate[mapOutlineNumPoints];
            for (int i = 0; i < mapOutlineNumPoints; i++)
            {
                mapOutline[i] = new Wgs84Coordinate(mapOutlineArray[i * 2 + 1], mapOutlineArray[i * 2]);
            }

            return new QctMetadata()
            {
                FileType = (QctType)await fs.ReadIntegerMetadata(0x00),
                LongTitle = await fs.ReadStringMetadata(0x10),
                Name = await fs.ReadStringMetadata(0x14),
                Identifier = await fs.ReadStringMetadata(0x18),
                Edition = await fs.ReadStringMetadata(0x1C),
                Revision = await fs.ReadStringMetadata(0x20),
                Keywords = await fs.ReadStringMetadata(0x24),
                Copyright = await fs.ReadStringMetadata(0x28),
                Scale = await fs.ReadStringMetadata(0x2C),
                Datum = await fs.ReadStringMetadata(0x30),
                Depths = await fs.ReadStringMetadata(0x34),
                Heights = await fs.ReadStringMetadata(0x38),
                Projection = await fs.ReadStringMetadata(0x3C),
                Flags = (QctFlags)await fs.ReadIntegerMetadata(0x40),
                MapType = await fs.ReadStringMetadata(edsPointer + 0x00),
                DatumShiftNorth = datumShift[0],
                DatumShiftEast = datumShift[1],
                MapOutline = mapOutline
            };
        }
    }
}
