using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OsMapDownloader.Qct
{
    public static class QctExtensions
    {
        public static async Task WriteIntegerMetadata(this FileStream fs, uint location, uint data, CancellationToken cancellationToken = default(CancellationToken))
        {
            fs.Seek(location, SeekOrigin.Begin);
            await fs.WriteAsync(BitConverter.GetBytes(data), cancellationToken);
        }

        public static async Task WriteDoubleMetadata(this FileStream fs, uint location, double data, CancellationToken cancellationToken = default(CancellationToken))
        {
            fs.Seek(location, SeekOrigin.Begin);
            await fs.WriteAsync(BitConverter.GetBytes(data), cancellationToken);
        }

        public static async Task<uint> WriteStringMetadata(this FileStream fs, uint location, string? data, uint pointerLocation, CancellationToken cancellationToken = default(CancellationToken))
        {
            //If the string is empty, don't write anything and write a 0 for the pointer. Do not increase the pointer location
            if (string.IsNullOrEmpty(data))
            {
                await fs.WriteIntegerMetadata(location, 0, cancellationToken);
                return pointerLocation;
            }

            //Firstly get the bytes from the string. Make sure it's null terminated
            byte[] dataB = Encoding.ASCII.GetBytes(data);
            byte[] dataBWithNull = new byte[dataB.Length + 1];
            dataB.CopyTo(dataBWithNull, 0);

            //Write the string to the pointer location
            fs.Seek(pointerLocation, SeekOrigin.Begin);
            await fs.WriteAsync(dataBWithNull, cancellationToken);

            //Write the pointer to the location
            await fs.WriteIntegerMetadata(location, pointerLocation, cancellationToken);

            //Increase the pointer and return
            pointerLocation += (uint)dataBWithNull.Length;
            return pointerLocation;
        }

        public static async Task<uint> WriteDoubleArrayMetadata(this FileStream fs, uint location, double[] data, uint pointerLocation, CancellationToken cancellationToken = default(CancellationToken))
        {
            //If the array is empty, don't write anything and write a 0 for the pointer. Do not increase the pointer location
            if (data.Length == 0)
            {
                await fs.WriteIntegerMetadata(location, 0, cancellationToken);
                return pointerLocation;
            }

            //Write each entry to pointer location
            fs.Seek(pointerLocation, SeekOrigin.Begin);
            foreach (double entry in data)
            {
                await fs.WriteAsync(BitConverter.GetBytes(entry), cancellationToken);
            }

            //Write the pointer to the location
            await fs.WriteIntegerMetadata(location, pointerLocation, cancellationToken);

            //Increase the pointer and return
            pointerLocation += 0x08 * (uint)data.Length;
            return pointerLocation;
        }

        public static async Task<uint> ReadIntegerMetadata(this FileStream fs, uint location, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] buffer = new byte[4];
            fs.Seek(location, SeekOrigin.Begin);
            await fs.ReadAsync(buffer, cancellationToken);
            return BitConverter.ToUInt32(buffer);
        }

        public static async Task<double> ReadDoubleMetadata(this FileStream fs, uint location, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] buffer = new byte[8];
            fs.Seek(location, SeekOrigin.Begin);
            await fs.ReadAsync(buffer, cancellationToken);
            return BitConverter.ToDouble(buffer);
        }

        public static async Task<string?> ReadStringMetadata(this FileStream fs, uint location, CancellationToken cancellationToken = default(CancellationToken))
        {
            uint pointer = await fs.ReadIntegerMetadata(location, cancellationToken);
            if (pointer == 0) return null;

            fs.Seek(pointer, SeekOrigin.Begin);
            List<byte> buffer = new List<byte>();
            byte lastChar = (byte)fs.ReadByte();
            while (lastChar != 0)
            {
                buffer.Add(lastChar);
                lastChar = (byte)fs.ReadByte();
            }
            return Encoding.ASCII.GetString(buffer.ToArray());
        }

        public static async Task<double[]> ReadDoubleArrayMetadata(this FileStream fs, uint location, int length, CancellationToken cancellationToken = default(CancellationToken))
        {
            uint pointer = await fs.ReadIntegerMetadata(location, cancellationToken);
            if (pointer == 0) return Array.Empty<double>();

            fs.Seek(pointer, SeekOrigin.Begin);
            double[] array = new double[length];
            byte[] buffer = new byte[8];
            for (int i = 0; i < length; i++)
            {
                await fs.ReadAsync(buffer, cancellationToken);
                array[i] = BitConverter.ToDouble(buffer);
            }
            return array;
        }
    }
}
