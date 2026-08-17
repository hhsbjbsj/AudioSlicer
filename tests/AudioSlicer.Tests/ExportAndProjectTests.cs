using System.IO;
using AudioSlicer.Export;
using AudioSlicer.Media;
using AudioSlicer.Models;
using AudioSlicer.Project;
using Xunit;

namespace AudioSlicer.Tests;

public sealed class ExportAndProjectTests
{
    [Fact]
    public async Task Exporter_AppliesDeleteAndMuteWithoutChangingSource()
    {
        var locator = new FFmpegLocator();
        var directory = Path.Combine(Path.GetTempPath(), $"AudioSlicerExportTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.wav");
        var output = Path.Combine(directory, "result.wav");
        try
        {
            await ProcessRunner.RunForTextAsync(locator.FFmpegPath, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:duration=2", source], CancellationToken.None);
            var originalLength = new FileInfo(source).Length;
            var segment = new Segment
            {
                Name = "test",
                StartTime = TimeSpan.FromSeconds(0.2),
                EndTime = TimeSpan.FromSeconds(1.8),
                DeletedRanges = [new AudioRange(TimeSpan.FromSeconds(0.7), TimeSpan.FromSeconds(0.8))],
                MutedRanges = [new AudioRange(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.1))],
            };
            await new AudioExporter(locator).ExportOneAsync(source, segment, output, new ExportSettings(), CancellationToken.None);
            var info = await new FFprobeService(locator).ProbeAsync(output, CancellationToken.None);
            Assert.InRange(info.Duration.TotalSeconds, 1.48, 1.51);
            Assert.Equal(originalLength, new FileInfo(source).Length);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectSerializer_RoundTripsFiveHundredSegments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.audioslice");
        try
        {
            var project = new AudioSliceProject { VideoPath = "video.mp4", VideoHash = "abc" };
            for (var index = 0; index < 500; index++)
            {
                project.Segments.Add(new Segment { Name = $"片段 {index}", StartTime = TimeSpan.FromMilliseconds(index * 100), EndTime = TimeSpan.FromMilliseconds(index * 100 + 55) });
            }
            var serializer = new ProjectSerializer();
            await serializer.SaveAsync(path, project, CancellationToken.None);
            var loaded = await serializer.LoadAsync(path, CancellationToken.None);
            Assert.Equal(500, loaded.Segments.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(55), loaded.Segments[499].Duration);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task BatchExporter_WritesWavFlacMp3AndZip()
    {
        var locator = new FFmpegLocator();
        var directory = Path.Combine(Path.GetTempPath(), $"AudioSlicerFormats-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.wav");
        try
        {
            await ProcessRunner.RunForTextAsync(locator.FFmpegPath, ["-y", "-f", "lavfi", "-i", "sine=frequency=330:duration=1", source], CancellationToken.None);
            var segment = new Segment { Name = "合法_文件名", StartTime = TimeSpan.FromSeconds(0.1), EndTime = TimeSpan.FromSeconds(0.9) };
            var exporter = new AudioExporter(locator);
            foreach (var format in Enum.GetValues<AudioExportFormat>())
            {
                var outputDirectory = Path.Combine(directory, format.ToString());
                var outputs = await exporter.ExportBatchAsync(source, [segment], outputDirectory, new ExportSettings { Format = format }, null, CancellationToken.None);
                Assert.Single(outputs);
                Assert.True(File.Exists(outputs[0]));
                Assert.True(new FileInfo(outputs[0]).Length > 100);
            }

            var zipPath = Path.Combine(directory, "segments.zip");
            await exporter.ExportZipAsync(source, [segment], zipPath, new ExportSettings(), null, CancellationToken.None);
            Assert.True(File.Exists(zipPath));
            Assert.True(new FileInfo(zipPath).Length > 100);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
