using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Waveform;

namespace AudioSlicer.ViewModels;

public sealed class WaveformViewModel : ObservableObject
{
    private WaveformData? _data;
    private double _viewportStartSeconds;
    private double _viewportDurationSeconds = 10;
    private double _selectionStartSeconds;
    private double _selectionEndSeconds;
    private bool _hasSelection;
    private string _selectionStartText = "00:00.000";
    private string _selectionEndText = "00:00.000";
    private bool _updatingText;

    public WaveformData? Data { get => _data; private set => SetProperty(ref _data, value); }
    public double DurationSeconds => Data?.Duration.TotalSeconds ?? 0;

    public double ViewportStartSeconds
    {
        get => _viewportStartSeconds;
        set => SetProperty(ref _viewportStartSeconds, Math.Clamp(value, 0, Math.Max(0, DurationSeconds - ViewportDurationSeconds)));
    }

    public double ViewportDurationSeconds
    {
        get => _viewportDurationSeconds;
        set => SetProperty(ref _viewportDurationSeconds, Math.Clamp(value, 0.01, Math.Max(0.01, DurationSeconds)));
    }

    public double SelectionStartSeconds
    {
        get => _selectionStartSeconds;
        set
        {
            var bounded = Math.Clamp(value, 0, DurationSeconds);
            if (SetProperty(ref _selectionStartSeconds, bounded))
            {
                HasSelection = SelectionEndSeconds > bounded;
                UpdateTimeTexts();
                OnPropertyChanged(nameof(SelectionDurationText));
            }
        }
    }

    public double SelectionEndSeconds
    {
        get => _selectionEndSeconds;
        set
        {
            var bounded = Math.Clamp(value, 0, DurationSeconds);
            if (SetProperty(ref _selectionEndSeconds, bounded))
            {
                HasSelection = bounded > SelectionStartSeconds;
                UpdateTimeTexts();
                OnPropertyChanged(nameof(SelectionDurationText));
            }
        }
    }

    public bool HasSelection { get => _hasSelection; private set => SetProperty(ref _hasSelection, value); }
    public string SelectionStartText { get => _selectionStartText; set => SetProperty(ref _selectionStartText, value); }
    public string SelectionEndText { get => _selectionEndText; set => SetProperty(ref _selectionEndText, value); }
    public string SelectionDurationText => FormatDuration(Math.Max(0, SelectionEndSeconds - SelectionStartSeconds));

    public void Load(WaveformData data)
    {
        Data = data;
        OnPropertyChanged(nameof(DurationSeconds));
        ViewportDurationSeconds = Math.Max(0.01, data.Duration.TotalSeconds);
        ViewportStartSeconds = 0;
        ClearSelection();
    }

    public void SetSelection(double startSeconds, double endSeconds)
    {
        var start = Math.Clamp(Math.Min(startSeconds, endSeconds), 0, DurationSeconds);
        var end = Math.Clamp(Math.Max(startSeconds, endSeconds), 0, DurationSeconds);
        SelectionStartSeconds = start;
        SelectionEndSeconds = end;
        HasSelection = end > start;
    }

    public bool ApplyTextSelection(out string? error)
    {
        if (!TryParseTime(SelectionStartText, out var start) || !TryParseTime(SelectionEndText, out var end))
        {
            error = "时间格式无效，请输入秒数或 hh:mm:ss.fff。";
            UpdateTimeTexts();
            return false;
        }

        if (start < 0 || end <= start || end > DurationSeconds)
        {
            error = "结束时间必须大于开始时间，且选区不能超出媒体时长。";
            UpdateTimeTexts();
            return false;
        }

        SetSelection(start, end);
        error = null;
        return true;
    }

    public void ClearSelection()
    {
        _selectionStartSeconds = 0;
        _selectionEndSeconds = 0;
        HasSelection = false;
        OnPropertyChanged(nameof(SelectionStartSeconds));
        OnPropertyChanged(nameof(SelectionEndSeconds));
        OnPropertyChanged(nameof(SelectionDurationText));
        UpdateTimeTexts();
    }

    private void UpdateTimeTexts()
    {
        if (_updatingText) return;
        _updatingText = true;
        SelectionStartText = FormatDuration(SelectionStartSeconds);
        SelectionEndText = FormatDuration(SelectionEndSeconds);
        _updatingText = false;
    }

    public static string FormatDuration(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : time.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static bool TryParseTime(string value, out double seconds)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) return true;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time))
        {
            seconds = time.TotalSeconds;
            return true;
        }
        seconds = 0;
        return false;
    }
}
