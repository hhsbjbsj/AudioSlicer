namespace AudioSlicer.Models;

public enum AudioExportFormat
{
    Wav,
    Flac,
    Mp3,
}

public sealed class ExportSettings
{
    public AudioExportFormat Format { get; set; } = AudioExportFormat.Wav;

    public int SampleRate { get; set; } = 48_000;

    public int Channels { get; set; } = 1;

    public int BitsPerSample { get; set; } = 16;

    public int Mp3BitRateKbps { get; set; } = 192;
}
