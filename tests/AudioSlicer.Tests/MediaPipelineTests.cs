using System.IO;
using System.Text;
using AudioSlicer.Media;
using AudioSlicer.Waveform;
using Xunit;

namespace AudioSlicer.Tests;

public sealed class MediaPipelineTests
{
    [Fact]
    public async Task ProbeAndWaveformGeneration_WorkForSyntheticMedia()
    {
        var locator = new FFmpegLocator();
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"AudioSlicerTests-中文路径-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var mediaPath = Path.Combine(temporaryDirectory, "出发.mp4");
        try
        {
            await ProcessRunner.RunForTextAsync(
                locator.FFmpegPath,
                ["-y", "-f", "lavfi", "-i", "color=c=black:s=320x180:d=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:v", "libx264", "-c:a", "aac", "-shortest", mediaPath],
                CancellationToken.None);

            var info = await new FFprobeService(locator).ProbeAsync(mediaPath, CancellationToken.None);
            Assert.True(info.HasAudio);
            Assert.True(info.HasVideo);
            Assert.InRange(info.Duration.TotalSeconds, 1.9, 2.1);

            var generator = new WaveformGenerator(locator, new WaveformCache());
            var result = await generator.GenerateAsync(mediaPath, info.Duration, null, CancellationToken.None);
            Assert.NotEmpty(result.Waveform.Levels);
            Assert.NotEmpty(result.Waveform.Levels[0].Peaks);
            Assert.Equal(48_000, result.Waveform.SampleRate);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProcessRunner_UsesUtf8ForRedirectedOutput()
    {
        var locator = new FFmpegLocator();
        using var process = ProcessRunner.CreateProcess(locator.FFprobePath, ["-version"], redirectStandardOutput: true);

        Assert.Equal(Encoding.UTF8.CodePage, process.StartInfo.StandardOutputEncoding?.CodePage);
        Assert.Equal(Encoding.UTF8.CodePage, process.StartInfo.StandardErrorEncoding?.CodePage);
    }
}
