using System.Drawing.Drawing2D;

namespace PcMonitorHost;

/// <summary>
/// Modern progress bar with gradient fill, rounded corners, and smooth rendering.
/// </summary>
internal sealed class ProgressBarEx : Control
{
    private float _value;
    private float _maximum = 100f;
    private Color _barColorStart = Color.FromArgb(0, 180, 120);
    private Color _barColorEnd = Color.FromArgb(0, 220, 160);
    private Color _trackColor = Color.FromArgb(40, 40, 40);
    private bool _showText = true;
    private string? _customText;

    public ProgressBarEx()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Height = 24;
    }

    public float Value
    {
        get => _value;
        set
        {
            _value = Math.Max(0f, Math.Min(_maximum, value));
            Invalidate();
        }
    }

    public float Maximum
    {
        get => _maximum;
        set
        {
            if (value > 0f)
            {
                _maximum = value;
                Invalidate();
            }
        }
    }

    public Color BarColorStart
    {
        get => _barColorStart;
        set { _barColorStart = value; Invalidate(); }
    }

    public Color BarColorEnd
    {
        get => _barColorEnd;
        set { _barColorEnd = value; Invalidate(); }
    }

    public Color TrackColor
    {
        get => _trackColor;
        set { _trackColor = value; Invalidate(); }
    }

    public bool ShowText
    {
        get => _showText;
        set { _showText = value; Invalidate(); }
    }

    public string? CustomText
    {
        get => _customText;
        set { _customText = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = 6;
        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        var trackPath = CreateRoundedRect(rect, radius);

        using (var trackBrush = new SolidBrush(_trackColor))
        {
            g.FillPath(trackBrush, trackPath);
        }

        if (_value > 0f && _maximum > 0f)
        {
            float ratio = _value / _maximum;
            float fillWidth = (Width - 1) * ratio;
            var fillRect = new RectangleF(0, 0, fillWidth, Height - 1);
            var fillPath = CreateRoundedRect(fillRect, radius);

            using (var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(fillWidth, 0),
                _barColorStart,
                _barColorEnd))
            {
                g.FillPath(brush, fillPath);
            }
        }

        if (_showText)
        {
            string text = _customText ?? $"{(int)_value}%";
            using var textBrush = new SolidBrush(ForeColor);
            using var font = new Font(Font.FontFamily, Font.Size * 0.85f, FontStyle.Bold);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, rect, fmt);
        }
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, int radius)
    {
        var path = new GraphicsPath();
        float r = radius;
        float diam = r * 2;

        path.AddArc(rect.X, rect.Y, diam, diam, 180, 90);
        path.AddArc(rect.Right - diam, rect.Y, diam, diam, 270, 90);
        path.AddArc(rect.Right - diam, rect.Bottom - diam, diam, diam, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diam, diam, diam, 90, 90);
        path.CloseFigure();

        return path;
    }
}
