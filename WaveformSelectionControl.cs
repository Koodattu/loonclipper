using System.Drawing.Drawing2D;

namespace LoonClipper;

internal sealed class WaveformSelectionControl : Control
{
    private static readonly Color AccentColor = Color.FromArgb(145, 70, 255);
    private static readonly TimeSpan MinimumSelection = TimeSpan.FromMilliseconds(100);
    private const int HandleHitWidth = 14;

    private Image? _waveformImage;
    private TimeSpan _duration;
    private TimeSpan _selectionStart;
    private TimeSpan _selectionEnd;
    private TimeSpan _playhead;
    private SelectionHandle _draggingHandle;
    private SelectionHandle _activeHandle = SelectionHandle.Start;

    public WaveformSelectionControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        AccessibleName = "Audio waveform trim selection";
        AccessibleDescription = "Drag the left and right purple handles to choose the audio to save.";
    }

    public event EventHandler? SelectionChanged;
    public event Action<TimeSpan>? SeekRequested;

    public TimeSpan Duration => _duration;
    public TimeSpan SelectionStart => _selectionStart;
    public TimeSpan SelectionEnd => _selectionEnd;

    public TimeSpan Playhead
    {
        get => _playhead;
        set
        {
            _playhead = Clamp(value, TimeSpan.Zero, _duration);
            Invalidate();
        }
    }

    public void SetWaveform(
        Image waveformImage,
        TimeSpan duration,
        TimeSpan? selectionStart = null,
        TimeSpan? selectionEnd = null)
    {
        _waveformImage?.Dispose();
        _waveformImage = waveformImage;
        _duration = duration;
        _selectionStart = Clamp(selectionStart ?? TimeSpan.Zero, TimeSpan.Zero, duration);
        _selectionEnd = Clamp(selectionEnd ?? duration, _selectionStart, duration);

        if (_selectionEnd - _selectionStart < MinimumSelection)
        {
            _selectionStart = TimeSpan.Zero;
            _selectionEnd = duration;
        }

        _playhead = _selectionStart;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        e.Graphics.Clear(Color.White);

        if (_waveformImage is null || _duration <= TimeSpan.Zero)
        {
            TextRenderer.DrawText(
                e.Graphics,
                "Preparing waveform…",
                Font,
                bounds,
                Color.FromArgb(92, 92, 102),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DrawOutline(e.Graphics, bounds);
            return;
        }

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_waveformImage, bounds);

        var startX = TimeToX(_selectionStart);
        var endX = TimeToX(_selectionEnd);

        using (var unselectedBrush = new SolidBrush(Color.FromArgb(178, 248, 248, 250)))
        {
            e.Graphics.FillRectangle(unselectedBrush, 0, 0, startX, Height);
            e.Graphics.FillRectangle(unselectedBrush, endX, 0, Width - endX, Height);
        }

        using (var selectedBrush = new SolidBrush(Color.FromArgb(20, AccentColor)))
        {
            e.Graphics.FillRectangle(selectedBrush, startX, 0, Math.Max(1, endX - startX), Height);
        }

        DrawHandle(e.Graphics, startX);
        DrawHandle(e.Graphics, endX);

        if (_playhead >= _selectionStart && _playhead <= _selectionEnd)
        {
            var playheadX = TimeToX(_playhead);
            using var playheadPen = new Pen(Color.FromArgb(205, 28, 28, 32), 2F);
            e.Graphics.DrawLine(playheadPen, playheadX, 0, playheadX, Height);
        }

        DrawOutline(e.Graphics, bounds);

        if (Focused)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -3, -3));
        }

        if (!Enabled)
        {
            using var disabledBrush = new SolidBrush(Color.FromArgb(90, BackColor));
            e.Graphics.FillRectangle(disabledBrush, bounds);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!Enabled || e.Button != MouseButtons.Left || _duration <= TimeSpan.Zero)
        {
            return;
        }

        Focus();
        var startDistance = Math.Abs(e.X - TimeToX(_selectionStart));
        var endDistance = Math.Abs(e.X - TimeToX(_selectionEnd));

        if (Math.Min(startDistance, endDistance) <= HandleHitWidth)
        {
            _draggingHandle = startDistance <= endDistance
                ? SelectionHandle.Start
                : SelectionHandle.End;
            _activeHandle = _draggingHandle;
            Capture = true;
            return;
        }

        var clickedTime = XToTime(e.X);
        if (clickedTime < _selectionStart)
        {
            _activeHandle = SelectionHandle.Start;
            _draggingHandle = SelectionHandle.Start;
            SetHandle(SelectionHandle.Start, clickedTime);
            Capture = true;
        }
        else if (clickedTime > _selectionEnd)
        {
            _activeHandle = SelectionHandle.End;
            _draggingHandle = SelectionHandle.End;
            SetHandle(SelectionHandle.End, clickedTime);
            Capture = true;
        }
        else
        {
            Playhead = clickedTime;
            SeekRequested?.Invoke(clickedTime);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingHandle != SelectionHandle.None)
        {
            SetHandle(_draggingHandle, XToTime(e.X));
            return;
        }

        var closeToHandle = Math.Abs(e.X - TimeToX(_selectionStart)) <= HandleHitWidth
                            || Math.Abs(e.X - TimeToX(_selectionEnd)) <= HandleHitWidth;
        Cursor = closeToHandle ? Cursors.SizeWE : Cursors.Hand;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingHandle = SelectionHandle.None;
        Capture = false;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        return key is Keys.Left or Keys.Right || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode is not (Keys.Left or Keys.Right) || _duration <= TimeSpan.Zero)
        {
            return;
        }

        var step = e.Control
            ? TimeSpan.FromMilliseconds(100)
            : e.Shift
                ? TimeSpan.FromSeconds(10)
                : TimeSpan.FromSeconds(1);
        var direction = e.KeyCode == Keys.Left ? -1 : 1;
        var current = _activeHandle == SelectionHandle.Start
            ? _selectionStart
            : _selectionEnd;

        SetHandle(_activeHandle, current + (step * direction));
        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _waveformImage?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetHandle(SelectionHandle handle, TimeSpan value)
    {
        if (handle == SelectionHandle.Start)
        {
            _selectionStart = Clamp(
                value,
                TimeSpan.Zero,
                _selectionEnd - MinimumSelection);
        }
        else
        {
            _selectionEnd = Clamp(
                value,
                _selectionStart + MinimumSelection,
                _duration);
        }

        _playhead = Clamp(_playhead, _selectionStart, _selectionEnd);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private int TimeToX(TimeSpan time)
    {
        if (_duration <= TimeSpan.Zero || Width <= 1)
        {
            return 0;
        }

        return (int)Math.Round(
            Math.Clamp(time.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1)
            * (Width - 1));
    }

    private TimeSpan XToTime(int x)
    {
        if (_duration <= TimeSpan.Zero || Width <= 1)
        {
            return TimeSpan.Zero;
        }

        var fraction = Math.Clamp((double)x / (Width - 1), 0, 1);
        return TimeSpan.FromTicks((long)Math.Round(_duration.Ticks * fraction));
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return value < minimum
            ? minimum
            : value > maximum
                ? maximum
                : value;
    }

    private static void DrawHandle(Graphics graphics, int x)
    {
        using var handlePen = new Pen(AccentColor, 4F);
        using var handleBrush = new SolidBrush(AccentColor);
        graphics.DrawLine(handlePen, x, 0, x, graphics.VisibleClipBounds.Height);
        graphics.FillRectangle(handleBrush, x - 5, 0, 10, 18);
        graphics.FillRectangle(handleBrush, x - 5, graphics.VisibleClipBounds.Height - 18, 10, 18);
    }

    private static void DrawOutline(Graphics graphics, Rectangle bounds)
    {
        using var outlinePen = new Pen(Color.FromArgb(26, 0, 0, 0));
        graphics.DrawRectangle(
            outlinePen,
            bounds.X,
            bounds.Y,
            bounds.Width - 1,
            bounds.Height - 1);
    }

    private enum SelectionHandle
    {
        None,
        Start,
        End
    }
}
