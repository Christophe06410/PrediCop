using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace PrediCop.BackOffice.Services;

[SupportedOSPlatform("windows")]
public class MapSnapshotService(IHttpClientFactory httpClientFactory, ILogger<MapSnapshotService> logger)
{
    private const int TileSize = 256;
    private const int GridSize = 5;
    private const int Zoom = 15;

    public async Task<byte[]?> GetMapSnapshotAsync(
        double lat, double lon,
        IEnumerable<StreetSegment> streets,
        CancellationToken ct = default)
    {
        if (Math.Abs(lat) < 0.001 && Math.Abs(lon) < 0.001)
            return null;

        try
        {
            var (centerTileX, centerTileY) = LatLonToTile(lat, lon, Zoom);
            int half = GridSize / 2;
            int startX = centerTileX - half;
            int startY = centerTileY - half;
            int imgSize = GridSize * TileSize;

            using var bitmap = new Bitmap(imgSize, imgSize);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.FromArgb(200, 200, 200));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var client = httpClientFactory.CreateClient("OsmTiles");

            for (int dy = 0; dy < GridSize; dy++)
            {
                for (int dx = 0; dx < GridSize; dx++)
                {
                    int tx = startX + dx;
                    int ty = startY + dy;
                    try
                    {
                        var bytes = await client.GetByteArrayAsync($"/{Zoom}/{tx}/{ty}.png", ct);
                        using var ms = new MemoryStream(bytes);
                        using var tile = Image.FromStream(ms);
                        g.DrawImage(tile, dx * TileSize, dy * TileSize, TileSize, TileSize);
                    }
                    catch
                    {
                        // leave gray square if tile unavailable
                    }
                }
            }

            // Draw street segments
            using var streetPen = new Pen(Color.FromArgb(200, 220, 60, 60), 3f);
            foreach (var s in streets)
            {
                var (px1, py1) = LatLonToPixel(s.StartLat, s.StartLon, startX, startY);
                var (px2, py2) = LatLonToPixel(s.EndLat, s.EndLon, startX, startY);
                if (InBounds(px1, py1, imgSize) || InBounds(px2, py2, imgSize))
                    g.DrawLine(streetPen, px1, py1, px2, py2);
            }

            // Draw POI marker
            var (poiX, poiY) = LatLonToPixel(lat, lon, startX, startY);
            const int r = 9;
            using var poiFill = new SolidBrush(Color.FromArgb(230, 220, 38, 38));
            using var poiBorder = new Pen(Color.White, 2.5f);
            g.FillEllipse(poiFill, poiX - r, poiY - r, r * 2, r * 2);
            g.DrawEllipse(poiBorder, poiX - r, poiY - r, r * 2, r * 2);

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MapSnapshotService failed for ({Lat},{Lon})", lat, lon);
            return null;
        }
    }

    private static (int x, int y) LatLonToTile(double lat, double lon, int zoom)
    {
        double n = Math.Pow(2, zoom);
        int x = (int)Math.Floor((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }

    private static (int px, int py) LatLonToPixel(double lat, double lon, int startTileX, int startTileY)
    {
        double n = Math.Pow(2, Zoom);
        double fx = (lon + 180.0) / 360.0 * n;
        double latRad = lat * Math.PI / 180.0;
        double fy = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return ((int)((fx - startTileX) * TileSize), (int)((fy - startTileY) * TileSize));
    }

    private static bool InBounds(int v, int v2, int size) =>
        v >= -50 && v <= size + 50 && v2 >= -50 && v2 <= size + 50;

    public record StreetSegment(double StartLat, double StartLon, double EndLat, double EndLon);
}
