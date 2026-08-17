using LibVLCSharp.Shared;

namespace AudioSlicer.Media;

public interface IVideoPlayerService : IDisposable
{
    MediaPlayer MediaPlayer { get; }

    bool HasMedia { get; }

    bool IsPlaying { get; }

    TimeSpan CurrentTime { get; set; }

    TimeSpan Duration { get; }

    int Volume { get; set; }

    float PlaybackRate { get; set; }

    Task OpenAsync(string filePath, CancellationToken cancellationToken);

    void Play();

    void Pause();

    void Stop();
}

