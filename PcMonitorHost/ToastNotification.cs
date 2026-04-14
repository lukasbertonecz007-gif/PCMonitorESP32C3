using System.Drawing.Drawing2D;

namespace PcMonitorHost;

/// <summary>
/// Toast notification that appears temporarily in the corner of a form.
/// </summary>
internal sealed class ToastNotification : Control
{
    private readonly Label _textLabel;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly System.Windows.Forms.Timer _hideTimer;

    private float _opacity = 1f;
    private bool _isShowing;

    public ToastNotification()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Visible = false;
        Size = new Size(280, 50);
        BackColor = Color.FromArgb(40, 40, 40);
        Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        _textLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(230, 230, 230),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
            Text = ""
        };

        Controls.Add(_textLabel);

        _fadeTimer = new System.Windows.Forms.Timer();
        _fadeTimer.Interval = 20;
        _fadeTimer.Tick += (_, _) => OnFadeTick();

        _hideTimer = new System.Windows.Forms.Timer();
        _hideTimer.Interval = 3000;
        _hideTimer.Tick += (_, _) => StartFadeOut();
    }

    public string Message
    {
        get => _textLabel.Text;
        set => _textLabel.Text = value;
    }

    public void Show(string message, int durationMs = 3000)
    {
        Message = message;
        _opacity = 0f;
        Visible = true;
        _isShowing = true;
        BringToFront();

        // Position in bottom-right corner of parent
        if (Parent != null)
        {
            Location = new Point(Parent.Width - Width - 16, Parent.Height - Height - 16);
        }

        // Fade in
        _hideTimer.Stop();
        _fadeTimer.Start();
        _hideTimer.Interval = durationMs;
    }

    private void OnFadeTick()
    {
        if (_opacity < 1f)
        {
            _opacity = Math.Min(1f, _opacity + 0.08f);
            Invalidate();
        }
        else
        {
            _fadeTimer.Stop();
            if (_isShowing)
            {
                _hideTimer.Start();
            }
        }
    }

    private void StartFadeOut()
    {
        _isShowing = false;
        _hideTimer.Stop();
        _fadeTimer.Interval = 30;
        _fadeTimer.Start();

        // Override tick for fade out
        _fadeTimer.Tick -= (_, _) => OnFadeTick();
        _fadeTimer.Tick += FadeOutTick;
    }

    private void FadeOutTick(object? sender, EventArgs e)
    {
        if (_opacity > 0f)
        {
            _opacity = Math.Max(0f, _opacity - 0.06f);
            Invalidate();
        }
        else
        {
            _fadeTimer.Stop();
            Visible = false;
            // Restore original tick
            _fadeTimer.Tick -= FadeOutTick;
            _fadeTimer.Tick += (_, _) => OnFadeTick();
            _fadeTimer.Interval = 20;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = 8;
        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        var path = CreateRoundedRect(rect, radius);

        // Background with opacity
        int alpha = (int)(_opacity * 230);
        using var bgBrush = new SolidBrush(Color.FromArgb(alpha, 40, 40, 40));
        g.FillPath(bgBrush, path);

        // Border
        using var borderPen = new Pen(Color.FromArgb(alpha, 80, 80, 80), 1f);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _hideTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
