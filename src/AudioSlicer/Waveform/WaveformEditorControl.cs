using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AudioSlicer.Waveform;

public sealed class WaveformEditorControl : FrameworkElement
{
    private enum DragMode { None, Select, StartHandle, EndHandle, Pan }
    private DragMode _dragMode;
    private Point _dragOrigin;
    private double _panOrigin;

    public static readonly DependencyProperty WaveformProperty = DependencyProperty.Register(
        nameof(Waveform), typeof(WaveformData), typeof(WaveformEditorControl), new FrameworkPropertyMetadata(null, Redraw));
    public static readonly DependencyProperty CurrentTimeSecondsProperty = Register(nameof(CurrentTimeSeconds), typeof(double), 0d);
    public static readonly DependencyProperty SelectionStartSecondsProperty = Register(nameof(SelectionStartSeconds), typeof(double), 0d);
    public static readonly DependencyProperty SelectionEndSecondsProperty = Register(nameof(SelectionEndSeconds), typeof(double), 0d);
    public static readonly DependencyProperty ViewportStartSecondsProperty = Register(nameof(ViewportStartSeconds), typeof(double), 0d);
    public static readonly DependencyProperty ViewportDurationSecondsProperty = Register(nameof(ViewportDurationSeconds), typeof(double), 10d);

    public WaveformData? Waveform { get => (WaveformData?)GetValue(WaveformProperty); set => SetValue(WaveformProperty, value); }
    public double CurrentTimeSeconds { get => (double)GetValue(CurrentTimeSecondsProperty); set => SetValue(CurrentTimeSecondsProperty, value); }
    public double SelectionStartSeconds { get => (double)GetValue(SelectionStartSecondsProperty); set => SetValue(SelectionStartSecondsProperty, value); }
    public double SelectionEndSeconds { get => (double)GetValue(SelectionEndSecondsProperty); set => SetValue(SelectionEndSecondsProperty, value); }
    public double ViewportStartSeconds { get => (double)GetValue(ViewportStartSecondsProperty); set => SetValue(ViewportStartSecondsProperty, value); }
    public double ViewportDurationSeconds { get => (double)GetValue(ViewportDurationSecondsProperty); set => SetValue(ViewportDurationSecondsProperty, value); }

    public WaveformEditorControl()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(250, 249, 255)), null, new Rect(RenderSize));
        if (Waveform is null || ActualWidth <= 1 || ActualHeight <= 1)
        {
            DrawCenteredText(context, "正在等待波形数据…");
            return;
        }

        const double timelineHeight = 24;
        var centerY = timelineHeight + (ActualHeight - timelineHeight) / 2;
        var amplitude = Math.Max(10, (ActualHeight - timelineHeight) * 0.46);
        DrawTimeline(context, timelineHeight);
        if (SelectionEndSeconds > SelectionStartSeconds)
        {
            var left = TimeToX(SelectionStartSeconds);
            var right = TimeToX(SelectionEndSeconds);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(48, 108, 99, 255)), null, new Rect(left, timelineHeight, right - left, ActualHeight - timelineHeight));
        }

        var level = Waveform.SelectLevel(ViewportDurationSeconds, ActualWidth);
        var startIndex = Math.Max(0, (int)Math.Floor(ViewportStartSeconds * Waveform.SampleRate / level.SamplesPerPeak));
        var endIndex = Math.Min(level.Peaks.Length, (int)Math.Ceiling((ViewportStartSeconds + ViewportDurationSeconds) * Waveform.SampleRate / level.SamplesPerPeak));
        var centerPen = new Pen(new SolidColorBrush(Color.FromRgb(231, 227, 244)), 1);
        context.DrawLine(centerPen, new Point(0, centerY), new Point(ActualWidth, centerY));
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(108, 99, 255)), 1);
        pen.Freeze();
        for (var index = startIndex; index < endIndex; index++)
        {
            var time = index * (double)level.SamplesPerPeak / Waveform.SampleRate;
            var x = TimeToX(time);
            var peak = level.Peaks[index];
            context.DrawLine(pen, new Point(x, centerY - peak.Maximum * amplitude), new Point(x, centerY - peak.Minimum * amplitude));
        }

        DrawMarker(context, SelectionStartSeconds, Color.FromRgb(67, 199, 215), 2);
        DrawMarker(context, SelectionEndSeconds, Color.FromRgb(67, 199, 215), 2);
        DrawMarker(context, CurrentTimeSeconds, Color.FromRgb(255, 94, 145), 2);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Waveform is null) return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            ViewportStartSeconds = ClampViewport(ViewportStartSeconds - Math.Sign(e.Delta) * ViewportDurationSeconds * 0.12);
        }
        else
        {
            var point = e.GetPosition(this);
            var anchor = XToTime(point.X);
            var duration = Math.Clamp(ViewportDurationSeconds * (e.Delta > 0 ? 0.72 : 1 / 0.72), 0.01, Waveform.Duration.TotalSeconds);
            ViewportDurationSeconds = duration;
            ViewportStartSeconds = ClampViewport(anchor - point.X / Math.Max(1, ActualWidth) * duration);
        }
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus(); CaptureMouse(); _dragOrigin = e.GetPosition(this); _panOrigin = ViewportStartSeconds;
        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right) { _dragMode = DragMode.Pan; Cursor = Cursors.Hand; }
        else if (e.ChangedButton == MouseButton.Left)
        {
            var startX = TimeToX(SelectionStartSeconds); var endX = TimeToX(SelectionEndSeconds);
            _dragMode = Math.Abs(_dragOrigin.X - startX) <= 7 ? DragMode.StartHandle : Math.Abs(_dragOrigin.X - endX) <= 7 ? DragMode.EndHandle : DragMode.Select;
            if (_dragMode == DragMode.Select) { SelectionStartSeconds = XToTime(_dragOrigin.X); SelectionEndSeconds = SelectionStartSeconds; CurrentTimeSeconds = SelectionStartSeconds; }
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragMode == DragMode.None) return;
        var point = e.GetPosition(this); var time = XToTime(point.X);
        if (_dragMode == DragMode.Select) { var origin = XToTime(_dragOrigin.X); SelectionStartSeconds = Math.Min(origin, time); SelectionEndSeconds = Math.Max(origin, time); }
        else if (_dragMode == DragMode.StartHandle) SelectionStartSeconds = Math.Min(time, SelectionEndSeconds - 0.000001);
        else if (_dragMode == DragMode.EndHandle) SelectionEndSeconds = Math.Max(time, SelectionStartSeconds + 0.000001);
        else if (_dragMode == DragMode.Pan) ViewportStartSeconds = ClampViewport(_panOrigin - (point.X - _dragOrigin.X) / Math.Max(1, ActualWidth) * ViewportDurationSeconds);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e) { _dragMode = DragMode.None; Cursor = Cursors.Cross; ReleaseMouseCapture(); e.Handled = true; }
    private double TimeToX(double time) => (time - ViewportStartSeconds) / Math.Max(0.000001, ViewportDurationSeconds) * ActualWidth;
    private double XToTime(double x) => Math.Clamp(ViewportStartSeconds + x / Math.Max(1, ActualWidth) * ViewportDurationSeconds, 0, Waveform?.Duration.TotalSeconds ?? 0);
    private double ClampViewport(double start) => Math.Clamp(start, 0, Math.Max(0, (Waveform?.Duration.TotalSeconds ?? 0) - ViewportDurationSeconds));

    private void DrawTimeline(DrawingContext context, double height)
    {
        var count = Math.Max(2, (int)(ActualWidth / 140)); var pen = new Pen(new SolidColorBrush(Color.FromRgb(221, 217, 236)), 1);
        for (var index = 0; index <= count; index++)
        {
            var x = index * ActualWidth / count; var time = ViewportStartSeconds + index * ViewportDurationSeconds / count;
            context.DrawLine(pen, new Point(x, height - 6), new Point(x, height));
            var text = new FormattedText(FormatTick(time), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 10, new SolidColorBrush(Color.FromRgb(125, 120, 151)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 2, Math.Max(2, ActualWidth - text.Width - 2)), 2));
        }
    }

    private void DrawMarker(DrawingContext context, double time, Color color, double thickness) { var x = TimeToX(time); if (x >= 0 && x <= ActualWidth) context.DrawLine(new Pen(new SolidColorBrush(color), thickness), new Point(x, 0), new Point(x, ActualHeight)); }
    private void DrawCenteredText(DrawingContext context, string value) { var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Variable Text"), 14, new SolidColorBrush(Color.FromRgb(125, 120, 151)), VisualTreeHelper.GetDpi(this).PixelsPerDip); context.DrawText(text, new Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2)); }
    private string FormatTick(double seconds) => ViewportDurationSeconds < 1 ? TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture) : TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture);
    private static DependencyProperty Register(string name, Type type, object? value) => DependencyProperty.Register(name, type, typeof(WaveformEditorControl), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Redraw));
    private static void Redraw(DependencyObject target, DependencyPropertyChangedEventArgs e) => ((WaveformEditorControl)target).InvalidateVisual();
}
