using System.IO;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace AudioSlicer.Media;

public sealed class LibVlcVideoPlayerService : IVideoPlayerService
{
    private readonly LibVLC _libVlc;
    private VlcMedia? _currentMedia;
    private bool _disposed;

    public LibVlcVideoPlayerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        MediaPlayer = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true,
            Volume = 100,
        };
    }

    public MediaPlayer MediaPlayer { get; }

    public bool HasMedia => _currentMedia is not null;

    public bool IsPlaying => MediaPlayer.IsPlaying;

    public TimeSpan CurrentTime
    {
        get => TimeSpan.FromMilliseconds(Math.Max(0, MediaPlayer.Time));
        set
        {
            if (!HasMedia)
            {
                return;
            }

            var bounded = Math.Clamp(value.TotalMilliseconds, 0, Duration.TotalMilliseconds);
            MediaPlayer.Time = (long)Math.Round(bounded, MidpointRounding.AwayFromZero);
        }
    }

    public TimeSpan Duration
    {
        get
        {
            var milliseconds = MediaPlayer.Length > 0
                ? MediaPlayer.Length
                : _currentMedia?.Duration ?? 0;

            return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        }
    }

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public float PlaybackRate
    {
        get => MediaPlayer.Rate;
        set
        {
            if (MediaPlayer.SetRate(Math.Clamp(value, 0.25f, 4.0f)) != 0)
            {
                throw new InvalidOperationException("当前媒体不支持调整播放速度。");
            }
        }
    }

    public async Task OpenAsync(string filePath, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到所选视频文件。", filePath);
        }

        using var media = new VlcMedia(_libVlc, new Uri(filePath));
        var parsedStatus = await media.Parse(MediaParseOptions.ParseLocal, -1, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (parsedStatus is MediaParsedStatus.Failed or MediaParsedStatus.Timeout)
        {
            throw new InvalidDataException("LibVLC 无法解析该媒体文件，文件可能已损坏或编码不受支持。");
        }

        var previousMedia = _currentMedia;
        _currentMedia = media.Duplicate();
        MediaPlayer.Media = _currentMedia;
        previousMedia?.Dispose();
    }

    public void Play()
    {
        if (HasMedia && !MediaPlayer.Play())
        {
            throw new InvalidOperationException("LibVLC 无法开始播放该媒体。");
        }
    }

    public void Pause()
    {
        if (HasMedia)
        {
            MediaPlayer.SetPause(true);
        }
    }

    public void Stop()
    {
        if (HasMedia)
        {
            MediaPlayer.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MediaPlayer.Stop();
        MediaPlayer.Dispose();
        _currentMedia?.Dispose();
        _libVlc.Dispose();
    }
}
