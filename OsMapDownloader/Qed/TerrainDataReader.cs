using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Qed
{
    internal class TerrainDataReader : IDisposable
    {
        private readonly Dictionary<string, double[]?> bngToData = new Dictionary<string, double[]?>();
        private readonly ZipArchive mainArchive;
        private bool disposedValue;

        public TerrainDataReader(string dataFileName)
        {
            mainArchive = new ZipArchive(File.OpenRead(dataFileName), ZipArchiveMode.Read);
        }

        public async Task<double> GetHeightAtPoint(Osgb36Coordinate point, CancellationToken cancellationToken = default(CancellationToken))
        {
            string bngSquare = point.GetBNGSquare();
            string squareId = bngSquare + ((int)Math.Floor(point.Easting % 100000) / 10000).ToString() + ((int)Math.Floor(point.Northing % 100000) / 10000).ToString();
            double[]? squareData;
            if (bngToData.ContainsKey(squareId))
            {
                squareData = bngToData[squareId];
            }
            else
            {
                //Find zip file inside zip, open that, pull the asc file out, then pull the numbers out of that
                ZipArchiveEntry? bngSquareFolder = mainArchive.Entries.FirstOrDefault(e => e.FullName.StartsWith($"data/{bngSquare.ToLower()}/{squareId.ToLower()}"));
                if (bngSquareFolder == null)
                    squareData = null;
                else
                {
                    using Stream subSquareStream = bngSquareFolder.Open();
                    using ZipArchive subSquareArchive = new ZipArchive(subSquareStream);
                    ZipArchiveEntry? dataFileEntry = subSquareArchive.GetEntry($"{squareId}.asc");
                    if (dataFileEntry == null)
                        squareData = null;
                    else
                    {
                        using Stream dataStream = dataFileEntry.Open();
                        byte[] dataBytes = new byte[dataFileEntry.Length];
                        await dataStream.ReadExactlyAsync(dataBytes, cancellationToken);
                        try
                        {
                            squareData = Encoding.UTF8.GetString(dataBytes).Split("\n").Skip(5).Take(200)
                                .SelectMany(line => line.Split(" ").Select(num => double.Parse(num))).ToArray();
                        }
                        catch (Exception e)
                        {
                            throw new MapGenerationException(MapGenerationExceptionReason.TerrainError, e);
                        }
                    }
                }
                bngToData[squareId] = squareData;
            }
            if (squareData == null) return 0;
            int x = (int)Math.Floor((point.Easting % 10000) / 50);
            int y = 199 - (int)Math.Floor((point.Northing % 10000) / 50);
            return squareData[(y * 200) + x];
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    mainArchive.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
