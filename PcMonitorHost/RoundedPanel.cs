using System.Drawing.Drawing2D;

namespace PcMonitorHost;

/// <summary>
/// Panel with rounded corners and optional border.
/// </summary>
internal sealed class RoundedPanel : Panel
{
    private int _cornerRadius = 10;
    private Color _borderColor = Color.FromArgb(50, 50, 50);
    private bool _showBorder = true;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.FromArgb(32, 32, 32);
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    public bool ShowBorder
    {
        get => _showBorder;
        set { _showBorder = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        var path = CreateRoundedRect(rect, _cornerRadius);

        // Fill background
        using var bgBrush = new SolidBrush(BackColor);
        g.FillPath(bgBrush, path);

        // Draw border
        if (_showBorder)
        {
            using var borderPen = new Pen(_borderColor, 1f);
            g.DrawPath(borderPen, path);
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
