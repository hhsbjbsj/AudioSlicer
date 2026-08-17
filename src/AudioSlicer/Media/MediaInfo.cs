namespace AudioSlicer.Media;

public sealed record MediaInfo(
    TimeSpan Duration,
    bool HasVideo,
    bool HasAudio,
    int AudioSampleRate,
    int AudioChannels,
    string AudioCodec,
    string VideoCodec,
    int Width,
    int Height);

