using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using AudioSlicer.Media;

namespace AudioSlicer.ViewModels;

public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IVideoPlayerService _playerService;
    private readonly DispatcherTimer _positionTimer;
    private double _currentTimeMilliseconds;
    private double _durationMilliseconds;
    private int _volume;
    private double _selectedPlaybackRate = 1.0;
    private bool _hasMedia;
    private bool _isPlaying;
    private bool _isUpdatingFromPlayer;
    private bool _isSeeking;
    private bool _disposed;
    private bool _isLooping;
    private double _loopStartMilliseconds;
    private double _loopEndMilliseconds;

    public PlayerViewModel(IVideoPlayerService playerService)
    {
        _playerService = playerService;
        _volume = playerService.Volume;
        PlaybackRates = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

        _positionTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _positionTimer.Tick += OnPositionTimerTick;
        _positionTimer.Start();
    }

    public MediaPlayer MediaPlayer => _playerService.MediaPlayer;

    public IReadOnlyList<double> PlaybackRates { get; }

    public bool HasMedia
    {
        get => _hasMedia;
        private set
        {
            if (SetProperty(ref _hasMedia, value))
            {
                TogglePlaybackCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseLabel));
            }
        }
    }

    public string PlayPauseLabel => IsPlaying ? "暂停" : "播放";

    public double CurrentTimeMilliseconds
    {
        get => _currentTimeMilliseconds;
        set
        {
            var maximum = Math.Max(0, DurationMilliseconds);
            var bounded = Math.Clamp(value, 0, maximum);
            if (SetProperty(ref _currentTimeMilliseconds, bounded))
            {
                OnPropertyChanged(nameof(CurrentTimeText));
                OnPropertyChanged(nameof(CurrentTimeSeconds));
                if (!_isUpdatingFromPlayer && HasMedia)
                {
                    _playerService.CurrentTime = TimeSpan.FromMilliseconds(bounded);
                }
            }
        }
    }

    public double CurrentTimeSeconds
    {
        get => CurrentTimeMilliseconds / 1000;
        set => CurrentTimeMilliseconds = value * 1000;
    }

    public double DurationMilliseconds
    {
        get => _durationMilliseconds;
        private set
        {
            if (SetProperty(ref _durationMilliseconds, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public string CurrentTimeText => FormatTime(TimeSpan.FromMilliseconds(CurrentTimeMilliseconds));

    public string DurationText => FormatTime(TimeSpan.FromMilliseconds(DurationMilliseconds));

    public int Volume
    {
        get => _volume;
        set
        {
            var bounded = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _volume, bounded))
            {
                _playerService.Volume = bounded;
            }
        }
    }

    public double SelectedPlaybackRate
    {
        get => _selectedPlaybackRate;
        set
        {
            if (SetProperty(ref _selectedPlaybackRate, value) && HasMedia)
            {
                _playerService.PlaybackRate = (float)value;
            }
        }
    }

    public bool IsLooping
    {
        get => _isLooping;
        private set => SetProperty(ref _isLooping, value);
    }

    public void ConfigureLoop(double startSeconds, double endSeconds, bool enabled)
    {
        _loopStartMilliseconds = startSeconds * 1000;
        _loopEndMilliseconds = endSeconds * 1000;
        IsLooping = enabled && endSeconds > startSeconds;
        if (IsLooping)
        {
            CurrentTimeMilliseconds = _loopStartMilliseconds;
            _playerService.Play();
        }
    }

    public void Step(double milliseconds)
    {
        CurrentTimeMilliseconds += milliseconds;
    }

    public async Task LoadAsync(string filePath, CancellationToken cancellationToken)
    {
        await _playerService.OpenAsync(filePath, cancellationToken);

        HasMedia = true;
        SelectedPlaybackRate = 1.0;
        RefreshFromPlayer(forcePositionUpdate: true);
    }

    public void BeginSeeking()
    {
        _isSeeking = true;
    }

    public void EndSeeking()
    {
        _isSeeking = false;
        if (HasMedia)
        {
            _playerService.CurrentTime = TimeSpan.FromMilliseconds(CurrentTimeMilliseconds);
        }
    }

    [RelayCommand(CanExecute = nameof(CanControlPlayback))]
    private void TogglePlayback()
    {
        if (_playerService.IsPlaying)
        {
            _playerService.Pause();
        }
        else
        {
            _playerService.Play();
        }

        RefreshFromPlayer(forcePositionUpdate: false);
    }

    [RelayCommand(CanExecute = nameof(CanControlPlayback))]
    private void Stop()
    {
        _playerService.Stop();
        RefreshFromPlayer(forcePositionUpdate: true);
    }

    private bool CanControlPlayback() => HasMedia;

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        RefreshFromPlayer(forcePositionUpdate: false);
    }

    private void RefreshFromPlayer(bool forcePositionUpdate)
    {
        IsPlaying = _playerService.IsPlaying;
        DurationMilliseconds = _playerService.Duration.TotalMilliseconds;

        if (IsLooping && _playerService.CurrentTime.TotalMilliseconds >= _loopEndMilliseconds)
        {
            _playerService.CurrentTime = TimeSpan.FromMilliseconds(_loopStartMilliseconds);
            if (!_playerService.IsPlaying) _playerService.Play();
        }

        if (_isSeeking && !forcePositionUpdate)
        {
            return;
        }

        _isUpdatingFromPlayer = true;
        try
        {
            CurrentTimeMilliseconds = _playerService.CurrentTime.TotalMilliseconds;
        }
        finally
        {
            _isUpdatingFromPlayer = false;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : time.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _positionTimer.Stop();
        _positionTimer.Tick -= OnPositionTimerTick;
        _playerService.Dispose();
    }
}
