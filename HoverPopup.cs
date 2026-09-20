using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AudioSourceTray;

sealed class HoverPopup : Form
{
    private AudioSnapshot _snapshot = AudioSnapshot.Empty;
    private float _scale = 1f;
    private readonly List<(AudioSource Source, Rectangle Bar)> _bars = [];
    private AudioSource? _dragging;

    public event Action<AudioSource, float>? VolumeChanged;

    public bool IsAdjustingVolume => _dragging is not null;

    public HoverPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        ControlBox = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        TopMost = true;
        BackColor = Color.FromArgb(22, 22, 24);
        DoubleBuffered = true;
        Padding = Padding.Empty;
        Text = "";
        Width = 1;
        Height = 1;
        Visible = false;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsCaption = 0x00C00000;
            const int WsThickFrame = 0x00040000;
            const int WsBorder = 0x00800000;
            const int WsExNoActivate = 0x08000000;
            const int WsExToolWindow = 0x00000080;
            const int WsExWindowEdge = 0x00000100;
            const int WsExClientEdge = 0x00000200;
            const int WsExDlgModalFrame = 0x00000001;
            const int WsExStaticEdge = 0x00020000;
            const int WsExLayered = 0x00080000;
            var cp = base.CreateParams;
            cp.Style &= ~(WsCaption | WsThickFrame | WsBorder);
            cp.ExStyle &= ~(WsExWindowEdge | WsExClientEdge | WsExDlgModalFrame | WsExStaticEdge | WsExLayered);
            cp.ExStyle |= WsExNoActivate | WsExToolWindow;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        const int DwmwaWindowCornerPreference = 33;
        const int DwmwcpRound = 2;
        var preference = DwmwcpRound;
        _ = DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HidePopup();
            return;
        }

        base.OnFormClosing(e);
    }

    public void SetSnapshot(AudioSnapshot snapshot)
    {
        if (_dragging is not null)
        {
            snapshot = new AudioSnapshot(
                snapshot.Sources.Select(source =>
                    SameSource(source, _dragging) ? source with { Volume = _dragging.Volume } : source).ToList(),
                snapshot.DefaultDevice);
        }

        _snapshot = snapshot;
        if (!Visible)
        {
            return;
        }

        RecalcSize();
        Invalidate();
    }

    public void ShowNear(Point screenPoint)
    {
        RecalcSize();
        var area = Screen.FromPoint(screenPoint).WorkingArea;
        var gap = Scale(12);
        var x = screenPoint.X - Width / 2;
        var y = screenPoint.Y - Height - gap;
        if (y < area.Top)
        {
            y = screenPoint.Y + Scale(20);
        }

        x = Math.Clamp(x, area.Left + 8, Math.Max(area.Left + 8, area.Right - Width - 8));
        y = Math.Clamp(y, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - Height - 8));
        Location = new Point(x, y);
        if (!Visible)
        {
            Show();
        }
        else
        {
            Invalidate();
        }
    }

    public void HidePopup()
    {
        _dragging = null;
        Capture = false;
        if (Visible)
        {
            Hide();
        }

        Location = new Point(-32000, -32000);
    }

    public bool ContainsScreenPoint(Point screenPoint)
        => Visible && Bounds.Contains(screenPoint);

    private void RecalcSize()
    {
        _scale = DeviceDpi / 96f;
        using var titleFont = TitleFont();
        using var appFont = AppFont();
        using var bodyFont = BodyFont();
        var width = Scale(280);
        var height = Scale(16);

        height += TextRenderer.MeasureText("PLAYING", titleFont).Height + Scale(10);

        if (!_snapshot.IsPlaying)
        {
            height += TextRenderer.MeasureText("No apps are playing audio", bodyFont).Height + Scale(8);
            if (!string.IsNullOrWhiteSpace(_snapshot.DefaultDevice))
            {
                height += TextRenderer.MeasureText(_snapshot.DefaultDevice, bodyFont).Height + Scale(4);
            }
        }
        else
        {
            foreach (var source in _snapshot.Sources)
            {
                height += Scale(8);
                height += TextRenderer.MeasureText(source.AppName, appFont, new Size(width - Scale(40), 0), TextFormat).Height;
                if (!string.IsNullOrWhiteSpace(source.Detail))
                {
                    height += TextRenderer.MeasureText(source.Detail, bodyFont, new Size(width - Scale(24), 0), TextFormat).Height;
                }

                height += TextRenderer.MeasureText(source.DeviceName, bodyFont, new Size(width - Scale(24), 0), TextFormat).Height;
                if (source.VolumeAdjustable)
                {
                    height += Scale(22);
                }
            }
        }

        height += Scale(16);
        Size = new Size(width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        using var border = new Pen(Color.FromArgb(60, 60, 64));
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using var titleFont = TitleFont();
        using var appFont = AppFont();
        using var bodyFont = BodyFont();

        var x = Scale(14);
        var y = Scale(12);
        var innerWidth = Width - Scale(28);
        var playing = _snapshot.IsPlaying;
        var titleColor = playing ? Color.FromArgb(50, 215, 75) : Color.FromArgb(142, 142, 147);
        var title = playing
            ? (_snapshot.Sources.Count == 1 ? "PLAYING" : $"PLAYING · {_snapshot.Sources.Count}")
            : "SILENT";

        TextRenderer.DrawText(g, title, titleFont, new Point(x, y), titleColor);
        y += TextRenderer.MeasureText(title, titleFont).Height + Scale(6);

        if (!playing)
        {
            TextRenderer.DrawText(
                g,
                "No apps are playing audio",
                bodyFont,
                new Rectangle(x, y, innerWidth, Scale(40)),
                Color.FromArgb(210, 210, 214),
                TextFormat);
            y += TextRenderer.MeasureText("No apps are playing audio", bodyFont).Height + Scale(4);
            if (!string.IsNullOrWhiteSpace(_snapshot.DefaultDevice))
            {
                TextRenderer.DrawText(
                    g,
                    _snapshot.DefaultDevice,
                    bodyFont,
                    new Rectangle(x, y, innerWidth, Scale(40)),
                    Color.FromArgb(120, 120, 124),
                    TextFormat);
            }

            return;
        }

        _bars.Clear();
        foreach (var source in _snapshot.Sources)
        {
            y += Scale(6);
            DrawPeak(g, x, y + Scale(5), source.Peak);
            TextRenderer.DrawText(
                g,
                source.AppName,
                appFont,
                new Rectangle(x + Scale(16), y, innerWidth - Scale(16), Scale(40)),
                Color.FromArgb(245, 245, 247),
                TextFormat);
            y += TextRenderer.MeasureText(source.AppName, appFont).Height;

            if (!string.IsNullOrWhiteSpace(source.Detail))
            {
                TextRenderer.DrawText(
                    g,
                    source.Detail,
                    bodyFont,
                    new Rectangle(x, y, innerWidth, Scale(60)),
                    Color.FromArgb(174, 174, 178),
                    TextFormat);
                y += TextRenderer.MeasureText(source.Detail, bodyFont, new Size(innerWidth, 0), TextFormat).Height;
            }

            TextRenderer.DrawText(
                g,
                source.DeviceName,
                bodyFont,
                new Rectangle(x, y, innerWidth, Scale(40)),
                Color.FromArgb(110, 110, 114),
                TextFormat);
            y += TextRenderer.MeasureText(source.DeviceName, bodyFont, new Size(innerWidth, 0), TextFormat).Height;

            if (source.VolumeAdjustable)
            {
                y += Scale(4);
                var volume = SameSource(source, _dragging) ? _dragging!.Volume : source.Volume;
                var percent = $"{Math.Clamp((int)Math.Round(volume * 100), 0, 100)}%";
                var percentSize = TextRenderer.MeasureText(percent, bodyFont);
                var barWidth = Math.Max(Scale(80), innerWidth - percentSize.Width - Scale(8));
                var barHeight = Scale(6);
                var bar = new Rectangle(x, y + Scale(4), barWidth, barHeight);
                DrawVolumeBar(g, bar, volume);
                TextRenderer.DrawText(
                    g,
                    percent,
                    bodyFont,
                    new Point(bar.Right + Scale(8), y),
                    Color.FromArgb(174, 174, 178));
                _bars.Add((source, Rectangle.Inflate(bar, Scale(2), Scale(8))));
                y += Scale(18);
            }
        }
    }

    private void DrawPeak(Graphics g, int x, int y, float peak)
    {
        var size = Scale(8);
        var alpha = (int)Math.Clamp(80 + peak * 175, 80, 255);
        using var brush = new SolidBrush(Color.FromArgb(alpha, 50, 215, 75));
        g.FillEllipse(brush, x, y, size, size);
    }

    private static void DrawVolumeBar(Graphics g, Rectangle bar, float volume)
    {
        volume = Math.Clamp(volume, 0f, 1f);
        using var track = new SolidBrush(Color.FromArgb(50, 50, 54));
        using var fill = new SolidBrush(Color.FromArgb(50, 215, 75));
        g.FillRectangle(track, bar);
        var fillWidth = Math.Max(0, (int)Math.Round(bar.Width * volume));
        if (fillWidth > 0)
        {
            g.FillRectangle(fill, bar.X, bar.Y, fillWidth, bar.Height);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        foreach (var (source, bar) in _bars)
        {
            if (!source.VolumeAdjustable || !bar.Contains(e.Location))
            {
                continue;
            }

            Capture = true;
            ApplyVolume(source, bar, e.X);
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging is null || (e.Button & MouseButtons.Left) == 0)
        {
            return;
        }

        foreach (var (source, bar) in _bars)
        {
            if (!SameSource(source, _dragging))
            {
                continue;
            }

            ApplyVolume(_dragging, bar, e.X);
            return;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = null;
            Capture = false;
        }
    }

    private void ApplyVolume(AudioSource source, Rectangle bar, int mouseX)
    {
        var volume = bar.Width <= 0 ? 0f : Math.Clamp((mouseX - bar.X) / (float)bar.Width, 0f, 1f);
        _dragging = source with { Volume = volume };
        VolumeChanged?.Invoke(source, volume);
        Invalidate();
    }

    private static bool SameSource(AudioSource left, AudioSource? right)
        => right is not null
           && left.ProcessId == right.ProcessId
           && string.Equals(left.DeviceName, right.DeviceName, StringComparison.OrdinalIgnoreCase)
           && string.Equals(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase);

    private static Font TitleFont() => new("Segoe UI", 8f, FontStyle.Bold);
    private static Font AppFont() => new("Segoe UI", 10f, FontStyle.Bold);
    private static Font BodyFont() => new("Segoe UI", 8.5f, FontStyle.Regular);
    private int Scale(int value) => (int)Math.Round(value * _scale);

    private const TextFormatFlags TextFormat =
        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
