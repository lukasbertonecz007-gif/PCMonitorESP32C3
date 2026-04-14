using System.Drawing.Drawing2D;

namespace PcMonitorHost;

/// <summary>
/// Telemetry card displaying a metric with label, value, progress bar, and optional sparkline.
/// </summary>
internal sealed class TelemetryCard : Control
{
    private readonly ProgressBarEx _progressBar;
    private SparklineGraph? _sparkline;
    private readonly Label _titleLabel;
    private readonly Label _valueLabel;
    private readonly Panel _contentPanel;

    private string _unit = "%";
    private float _value;
    private float _maximum = 100f;
    private Color _accentColor = Color.FromArgb(0, 200, 140);
    private bool _showSparkline;

    public TelemetryCard()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Size = new Size(200, 100);
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.FromArgb(230, 230, 230);

        _titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 180, 180),
            Text = "Metric",
            Padding = new Padding(8, 2, 0, 0)
        };

        _valueLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = ForeColor,
            Text = "0%",
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _progressBar = new ProgressBarEx
        {
            Dock = DockStyle.Top,
            Height = 18,
            ShowText = false,
            Margin = new Padding(6, 2, 6, 2)
        };

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        _contentPanel.Controls.Add(_progressBar);
        _contentPanel.Controls.Add(_valueLabel);
        _contentPanel.Controls.Add(_titleLabel);

        Controls.Add(_contentPanel);
    }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public float Value
    {
        get => _value;
        set
        {
            _value = value;
            UpdateDisplay();
        }
    }

    public float Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            _progressBar.Maximum = value;
        }
    }

    public string Unit
    {
        get => _unit;
        set
        {
            _unit = value;
            UpdateDisplay();
        }
    }

    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            _progressBar.BarColorStart = Color.FromArgb(
                (int)(_accentColor.R * 0.7f),
                (int)(_accentColor.G * 0.7f),
                (int)(_accentColor.B * 0.7f));
            _progressBar.BarColorEnd = _accentColor;
            _valueLabel.ForeColor = _accentColor;
            if (_sparkline != null)
            {
                _sparkline.LineColor = _accentColor;
            }
        }
    }

    public bool ShowSparkline
    {
        get => _showSparkline;
        set
        {
            _showSparkline = value;
            if (_showSparkline && _sparkline == null)
            {
                _sparkline = new SparklineGraph
                {
                    Dock = DockStyle.Fill,
                    Height = 30,
                    Maximum = _maximum,
                    LineColor = _accentColor,
                    ShowGrid = false
                };
                Controls.Add(_sparkline);
                _sparkline.BringToFront();
                _sparkline.Location = new Point(0, Height - 30);
                _sparkline.Size = new Size(Width, 30);
            }
            else if (!_showSparkline && _sparkline != null)
            {
                Controls.Remove(_sparkline);
                _sparkline.Dispose();
            }
            Invalidate();
        }
    }

    public void AddSparklineValue(float value)
    {
        if (_sparkline != null)
        {
            _sparkline.AddValue(value);
        }
    }

    public void ClearSparkline()
    {
        _sparkline?.Clear();
    }

    private void UpdateDisplay()
    {
        _progressBar.Value = _value;
        _progressBar.Maximum = _maximum;
        if (_sparkline != null)
        {
            _sparkline.Maximum = _maximum;
        }

        string displayValue = _value < 0 ? "N/A" : FormatValue(_value);
        _valueLabel.Text = $"{displayValue}{_unit}";
    }

    private string FormatValue(float v)
    {
        if (_unit == "°C" || _unit == "W")
        {
            return ((int)Math.Round(v)).ToString();
        }
        if (_unit == "GB")
        {
            return v.ToString("0.0");
        }
        if (_unit == "MB/s")
        {
            return v.ToString("0.0");
        }
        return ((int)Math.Round(v)).ToString();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_sparkline != null)
        {
            _sparkline.Location = new Point(0, Height - 30);
            _sparkline.Size = new Size(Width, 30);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw rounded border
        int radius = 8;
        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        var path = CreateRoundedRect(rect, radius);

        using var borderPen = new Pen(Color.FromArgb(50, 50, 50), 1f);
        g.DrawPath(borderPen, path);
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
