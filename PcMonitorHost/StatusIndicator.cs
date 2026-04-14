namespace PcMonitorHost;

/// <summary>
/// Status indicator with colored dot and label for the status bar.
/// </summary>
internal sealed class StatusIndicator : Control
{
    private readonly Label _label;
    private Color _dotColor = Color.Gray;
    private string _text = "";

    public StatusIndicator()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Height = 24;
        Width = 180;

        _label = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(200, 200, 200),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Text = ""
        };

        Controls.Add(_label);
    }

    public Color DotColor
    {
        get => _dotColor;
        set
        {
            _dotColor = value;
            Invalidate();
        }
    }

    public new string Text
    {
        get => _text;
        set
        {
            _text = value;
            _label.Text = value;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw dot
        using var dotBrush = new SolidBrush(_dotColor);
        g.FillEllipse(dotBrush, 4, 7, 10, 10);

        // Draw border around dot
        using var pen = new Pen(Color.FromArgb(60, 60, 60), 1f);
        g.DrawEllipse(pen, 4, 7, 10, 10);
    }
}
