namespace AudioSlicer.Waveform;

public readonly record struct PeakPair(float Minimum, float Maximum);

public sealed record WaveformLevel(int SamplesPerPeak, PeakPair[] Peaks);

public sealed record WaveformData(
    int SampleRate,
    long TotalSamples,
    TimeSpan Duration,
    IReadOnlyList<WaveformLevel> Levels)
{
    public WaveformLevel SelectLevel(double visibleSeconds, double pixelWidth)
    {
        if (Levels.Count == 0)
        {
            throw new InvalidOperationException("波形缓存中没有可用层级。");
        }

        var samplesPerPixel = visibleSeconds * SampleRate / Math.Max(1, pixelWidth);
        return Levels
            .OrderBy(level => Math.Abs(Math.Log(Math.Max(1, level.SamplesPerPeak) / Math.Max(1, samplesPerPixel))))
            .First();
    }
}

