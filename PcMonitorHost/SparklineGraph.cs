namespace PcMonitorHost;

/// <summary>
/// Sparkline graph for visualizing time-series data (network, disk, etc.).
/// </summary>
internal sealed class SparklineGraph : Control
{
    private readonly List<float> _values = new();
    private Color _lineColor = Color.FromArgb(0, 200, 140);
    private Color _fillColor = Color.FromArgb(30, 0, 200, 140);
    private float _maximum = 100f;
    private bool _showGrid = true;
    private Color _gridColor = Color.FromArgb(50, 50, 50);

    public SparklineGraph()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Height = 60;
    }

    public IReadOnlyList<float> Values => _values;

    public Color LineColor
    {
        get => _lineColor;
        set { _lineColor = value; Invalidate(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
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

    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; Invalidate(); }
    }

    public void AddValue(float value)
    {
        _values.Add(Math.Max(0f, value));
        Invalidate();
    }

    public void SetValues(IEnumerable<float> values)
    {
        _values.Clear();
        _values.AddRange(values);
        Invalidate();
    }

    public void Clear()
    {
        _values.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int w = Width;
        int h = Height;

        // Draw grid lines
        if (_showGrid && _values.Count > 0)
        {
            using var gridPen = new Pen(_gridColor, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                int y = (h / 4) * i;
                g.DrawLine(gridPen, 0, y, w, y);
            }
        }

        if (_values.Count < 2)
        {
            return;
        }

        float maxVal = _maximum > 0f ? _maximum : _values.Max();
        if (maxVal <= 0f)
        {
            maxVal = 100f;
        }

        int count = _values.Count;
        float stepX = (float)w / Math.Max(count - 1, 1);

        // Build points
        var points = new List<PointF>(count);
        for (int i = 0; i < count; i++)
        {
            float x = i * stepX;
            float y = h - (_values[i] / maxVal) * h;
            points.Add(new PointF(x, y));
        }

        // Fill area under line
        var fillPoints = new List<PointF>(points);
        fillPoints.Add(new PointF((count - 1) * stepX, h));
        fillPoints.Add(new PointF(0, h));

        using (var fillBrush = new SolidBrush(_fillColor))
        {
            g.FillPolygon(fillBrush, fillPoints.ToArray());
        }

        // Draw line
        using var linePen = new Pen(_lineColor, 1.5f);
        for (int i = 1; i < points.Count; i++)
        {
            g.DrawLine(linePen, points[i - 1], points[i]);
        }

        // Draw end dot
        if (points.Count > 0)
        {
            var lastPt = points[^1];
            using var dotBrush = new SolidBrush(_lineColor);
            g.FillEllipse(dotBrush, lastPt.X - 2, lastPt.Y - 2, 4, 4);
        }
    }
}
