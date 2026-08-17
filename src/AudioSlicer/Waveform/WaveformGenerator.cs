using System.Diagnostics;
using System.IO;
using AudioSlicer.Media;
using NAudio.Wave;

namespace AudioSlicer.Waveform;

public sealed class WaveformGenerator(FFmpegLocator locator, WaveformCache cache)
{
    private const int SampleRate = 48_000;
    private const int BaseSamplesPerPeak = 64;
    private const int PyramidFactor = 8;

    public async Task<(WaveformData Waveform, string MediaHash, bool FromCache)> GenerateAsync(
        string mediaPath,
        TimeSpan duration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(mediaPath);
        var hash = await cache.ComputeMediaHashAsync(mediaPath, cancellationToken);
        var cached = await cache.TryLoadAsync(hash, file, cancellationToken);
        if (cached is not null)
        {
            progress?.Report(1);
            return (cached, hash, true);
        }

        var basePeaks = await DecodePeaksAsync(mediaPath, duration, progress, cancellationToken);
        var levels = BuildPyramid(basePeaks);
        var totalSamples = (long)Math.Round(duration.TotalSeconds * SampleRate);
        var waveform = new WaveformData(SampleRate, totalSamples, duration, levels);
        await cache.SaveAsync(hash, file, waveform, cancellationToken);
        return (waveform, hash, false);
    }

    private async Task<PeakPair[]> DecodePeaksAsync(
        string mediaPath,
        TimeSpan duration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var process = ProcessRunner.CreateProcess(
            locator.FFmpegPath,
            ["-hide_banner", "-loglevel", "error", "-i", mediaPath, "-map", "0:a:0", "-vn", "-ac", "1", "-ar", SampleRate.ToString(), "-f", "f32le", "pipe:1"],
            redirectStandardOutput: true);
        process.Start();
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var peaks = new List<PeakPair>(Math.Max(1024, (int)Math.Min(int.MaxValue, duration.TotalSeconds * SampleRate / BaseSamplesPerPeak)));
        var decodedFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var bytesPerSample = decodedFormat.BitsPerSample / 8;
        var byteBuffer = new byte[64 * 1024];
        var sampleBuffer = new byte[bytesPerSample];
        var sampleBufferCount = 0;
        var blockCount = 0;
        var minimum = 1f;
        var maximum = -1f;
        long samplesRead = 0;

        try
        {
            while (true)
            {
                var read = await process.StandardOutput.BaseStream.ReadAsync(byteBuffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                for (var byteIndex = 0; byteIndex < read; byteIndex++)
                {
                    sampleBuffer[sampleBufferCount++] = byteBuffer[byteIndex];
                    if (sampleBufferCount != bytesPerSample)
                    {
                        continue;
                    }

                    var sample = Math.Clamp(BitConverter.ToSingle(sampleBuffer), -1f, 1f);
                    sampleBufferCount = 0;
                    minimum = Math.Min(minimum, sample);
                    maximum = Math.Max(maximum, sample);
                    blockCount++;
                    samplesRead++;
                    if (blockCount == BaseSamplesPerPeak)
                    {
                        peaks.Add(new PeakPair(minimum, maximum));
                        blockCount = 0;
                        minimum = 1f;
                        maximum = -1f;
                    }
                }

                if (duration.TotalSeconds > 0)
                {
                    progress?.Report(Math.Clamp(samplesRead / (duration.TotalSeconds * SampleRate), 0, 0.99));
                }
            }

            if (blockCount > 0)
            {
                peaks.Add(new PeakPair(minimum, maximum));
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ProcessRunner.TryKill(process);
            throw;
        }

        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg 解码音频失败：{error.Trim()}");
        }

        progress?.Report(1);
        return [.. peaks];
    }

    private static IReadOnlyList<WaveformLevel> BuildPyramid(PeakPair[] basePeaks)
    {
        var levels = new List<WaveformLevel>();
        var current = basePeaks;
        var samplesPerPeak = BaseSamplesPerPeak;
        levels.Add(new WaveformLevel(samplesPerPeak, current));

        while (current.Length > 2_000)
        {
            var next = new PeakPair[(current.Length + PyramidFactor - 1) / PyramidFactor];
            for (var outputIndex = 0; outputIndex < next.Length; outputIndex++)
            {
                var minimum = 1f;
                var maximum = -1f;
                var end = Math.Min(current.Length, (outputIndex + 1) * PyramidFactor);
                for (var inputIndex = outputIndex * PyramidFactor; inputIndex < end; inputIndex++)
                {
                    minimum = Math.Min(minimum, current[inputIndex].Minimum);
                    maximum = Math.Max(maximum, current[inputIndex].Maximum);
                }

                next[outputIndex] = new PeakPair(minimum, maximum);
            }

            samplesPerPeak *= PyramidFactor;
            current = next;
            levels.Add(new WaveformLevel(samplesPerPeak, current));
        }

        return levels;
    }
}
