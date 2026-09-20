using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AudioSourceTray;

static class AppIcons
{
    public static Icon Idle { get; } = Create(playing: false);
    public static Icon Playing { get; } = Create(playing: true);

    private static Icon Create(bool playing)
    {
        using var bitmap = Draw(32, playing);
        using var stream = new MemoryStream();
        WriteIco(stream, bitmap);
        stream.Position = 0;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static Bitmap Draw(int size, bool playing)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var color = playing ? Color.FromArgb(50, 215, 75) : Color.FromArgb(236, 236, 239);
        using var brush = new SolidBrush(color);

        var s = size;
        var heights = playing ? new[] { 0.55f, 1.00f, 0.72f } : new[] { 0.38f, 0.70f, 0.50f };
        var barWidth = s * 0.18f;
        var gap = s * 0.09f;
        var total = barWidth * 3 + gap * 2;
        var x0 = (s - total) / 2f;
        var bottom = s * 0.86f;
        var maxH = s * 0.72f;
        var radius = Math.Max(1.5f, barWidth / 2.2f);

        for (var i = 0; i < 3; i++)
        {
            var h = maxH * heights[i];
            var x = x0 + i * (barWidth + gap);
            var y = bottom - h;
            using var path = RoundedRect(x, y, barWidth, h, radius);
            g.FillPath(brush, path);
        }

        return bitmap;
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2f);
        var path = new GraphicsPath();
        var d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void WriteIco(Stream stream, Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        var pngBytes = png.ToArray();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((byte)bitmap.Width);
        writer.Write((byte)bitmap.Height);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(pngBytes.Length);
        writer.Write(22);
        writer.Write(pngBytes);
    }
}
