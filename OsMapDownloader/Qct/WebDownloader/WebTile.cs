using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Qct.WebDownloader
{
    public class WebTile
    {
        public IntCoordinate TopLeft { get; }
        public Scale MapScale { get; }
        public byte Zoom { get; }
        public string FileName { get => $"{(int)MapScale}_{TopLeft.X}_{TopLeft.Y}"; }

        public WebTile(int x, int y, Scale mapScale) : this(new IntCoordinate(x, y), mapScale) { }
        public WebTile(Wgs84Coordinate lonLat, Scale mapScale) : this(GetTileFromLonLat(lonLat, GetMaxZoomFromScale(mapScale)), mapScale) { }
        public WebTile(IntCoordinate position, Scale mapScale) : this(position, mapScale, GetMaxZoomFromScale(mapScale)) { }
        public WebTile(IntCoordinate position, Scale mapScale, byte zoom)
        {
            TopLeft = position;
            MapScale = mapScale;
            Zoom = zoom;
        }

        public static IntCoordinate GetTileFromLonLat(Wgs84Coordinate lonLat, byte zoom)
        {
            Coordinate returnVal = GetTileFromLonLatExact(lonLat, zoom);
            return new IntCoordinate((int)returnVal.X, (int)returnVal.Y);
        }

        public static Coordinate GetPixelsFromLonLatExact(Wgs84Coordinate lonLat, byte zoom)
        {
            return GetTileFromLonLatExact(lonLat, zoom) * 256;
        }

        public static Coordinate GetTileFromLonLatExact(Wgs84Coordinate lonLat, byte zoom)
        {
            Coordinate proportions = LonLatToProportions(lonLat);
            return proportions * Math.Pow(2, zoom);
        }

        private static Coordinate LonLatToProportions(Wgs84Coordinate lonLat)
        {
            double x = (180.0 + lonLat.Longitude) / 360.0;
            double y = (180.0 - 180.0 / Math.PI * Math.Log(Math.Tan(Math.PI / 4.0 + lonLat.Latitude * (Math.PI / 360.0)))) / 360.0;
            return new Coordinate(x, y);
        }

        public static Wgs84Coordinate GetLonLatFromPixelsExact(Coordinate pixels, byte zoom)
        {
            return GetLonLatFromTileExact(pixels / 256, zoom);
        }

        public static Wgs84Coordinate GetLonLatFromTileExact(Coordinate tile, byte zoom)
        {
            Coordinate proportions = tile / Math.Pow(2, zoom);
            return ProportionsToLonLat(proportions);
        }

        private static Wgs84Coordinate ProportionsToLonLat(Coordinate proportions)
        {
            double lon = 360.0 * proportions.X - 180.0;
            double lat = (Math.Atan(Math.Pow(Math.E, (180.0 - 360.0 * proportions.Y) / (180.0 / Math.PI))) - Math.PI / 4.0) / (Math.PI / 360.0);
            return new Wgs84Coordinate(lon, lat);
        }

        public static byte GetMaxZoomFromScale(Scale scale)
        {
            return scale switch
            {
                Scale.Explorer or Scale.Landranger => 16,
                Scale.Road => 12,
                Scale.MiniScale => 10,
                _ => throw new ArgumentException("Scale has an invalid value")
            };
        }

        public async Task<byte[]?> DownloadAsync(HttpClient client, string token, CancellationToken cancellationToken = default)
        {
            byte[]? data;
            try
            {
                string url = MapScale switch
                {
                    Scale.Explorer => $"https://tiles-web-leisure.oscptiles.com/1_25k/{Zoom}/{TopLeft.X}/{TopLeft.Y}.png?token={token}",
                    Scale.Landranger => $"https://tiles-web-leisure.oscptiles.com/1_50k/{Zoom}/{TopLeft.X}/{TopLeft.Y}.png?token={token}",
                    Scale.Road => $"https://250k-tiles-web-leisure.oscptiles.com/{Zoom}/{TopLeft.X}/{TopLeft.Y}.png?token={token}",
                    Scale.MiniScale => $"https://250k-tiles-web-leisure.oscptiles.com/{Zoom}/{TopLeft.X}/{TopLeft.Y}.png?token={token}",
                    _ => throw new ArgumentException("Scale has an invalid value")
                };
                data = await client.GetByteArrayAsync(url, cancellationToken);
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    //We're way out at sea, so it's null
                    data = null;
                }
                else
                {
                    //Something else went wrong, throw exception
                    throw;
                }
            }

            return data;
        }

        public static async Task<string> GetToken(HttpClient client, CancellationToken cancellationToken = default)
        {
            string html = await client.GetStringAsync("https://explore.osmaps.com/", cancellationToken);
            const string START_TAG = "<script id=\"__NEXT_DATA__\" type=\"application/json\">";
            const string END_TAG = "</script>";
            string? token = null;
            int jsonStartIndex = html.IndexOf(START_TAG) + START_TAG.Length;
            int jsonEndIndex = html.IndexOf(END_TAG, jsonStartIndex);
            if (jsonStartIndex >= 0 && jsonEndIndex >= 0)
            {
                JsonNextData? nextData = JsonSerializer.Deserialize<JsonNextData>(html[jsonStartIndex..jsonEndIndex]);
                if (nextData?.runtimeConfig != null && nextData.runtimeConfig.ContainsKey("NEXT_PUBLIC_MAPBOX_LEISURE_STYLE_TOKEN"))
                    token = nextData.runtimeConfig["NEXT_PUBLIC_MAPBOX_LEISURE_STYLE_TOKEN"];
            }
            if (token == null)
            {
                throw new HttpRequestException("Failed to retrieve the token. OS maps has probably changed something on their website that broke this.");
            }

            return token.Replace("?token=", "");
        }

        private class JsonNextData
        {
            public Dictionary<string, string>? runtimeConfig { get; set; }
            //There are a few other properties but they're not needed
        }
    }
}
