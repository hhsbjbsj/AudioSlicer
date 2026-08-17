using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using AudioSlicer.Media;
using AudioSlicer.Models;

namespace AudioSlicer.Export;

public sealed class AudioExporter(FFmpegLocator locator)
{
    public async Task<IReadOnlyList<string>> ExportBatchAsync(
        string mediaPath,
        IReadOnlyList<Segment> segments,
        string outputDirectory,
        ExportSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (segments.Count == 0) throw new InvalidOperationException("没有选中需要导出的片段。");
        Directory.CreateDirectory(outputDirectory);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(outputDirectory))!);
        if (drive.AvailableFreeSpace < 64L * 1024 * 1024) throw new IOException("目标磁盘剩余空间不足 64 MB，无法安全导出。");

        var outputs = new List<string>(segments.Count);
        for (var index = 0; index < segments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = settings.Format.ToString().ToLowerInvariant();
            var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(segments[index].Name) ? $"{index + 1:0000}" : segments[index].Name);
            var outputPath = FindAvailablePath(outputDirectory, baseName, extension);
            await ExportOneAsync(mediaPath, segments[index], outputPath, settings, cancellationToken);
            outputs.Add(outputPath);
            progress?.Report((index + 1d) / segments.Count);
        }
        return outputs;
    }

    public async Task ExportOneAsync(string mediaPath, Segment segment, string outputPath, ExportSettings settings, CancellationToken cancellationToken)
    {
        if (segment.EndTime <= segment.StartTime) throw new InvalidOperationException($"片段“{segment.Name}”的时间范围无效。");
        var temporaryPath = outputPath + ".partial";
        var filter = BuildFilter(segment, settings.SampleRate);
        var arguments = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error", "-i", mediaPath,
            "-filter_complex", filter, "-map", "[out]", "-vn", "-ar", settings.SampleRate.ToString(CultureInfo.InvariantCulture),
            "-ac", settings.Channels.ToString(CultureInfo.InvariantCulture),
        };
        if (settings.Format == AudioExportFormat.Wav) arguments.AddRange(["-c:a", settings.BitsPerSample == 24 ? "pcm_s24le" : "pcm_s16le"]);
        else if (settings.Format == AudioExportFormat.Flac) arguments.AddRange(["-c:a", "flac"]);
        else arguments.AddRange(["-c:a", "libmp3lame", "-b:a", $"{settings.Mp3BitRateKbps}k"]);
        arguments.Add("-f");
        arguments.Add(settings.Format == AudioExportFormat.Wav ? "wav" : settings.Format == AudioExportFormat.Flac ? "flac" : "mp3");
        arguments.Add(temporaryPath);

        try
        {
            await ProcessRunner.RunForTextAsync(locator.FFmpegPath, arguments, cancellationToken);
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public async Task ExportZipAsync(
        string mediaPath,
        IReadOnlyList<Segment> segments,
        string zipPath,
        ExportSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"AudioSlicerExport-{Guid.NewGuid():N}");
        var temporaryZip = zipPath + ".partial";
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            await ExportBatchAsync(mediaPath, segments, temporaryDirectory, settings, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(temporaryZip)) File.Delete(temporaryZip);
            ZipFile.CreateFromDirectory(temporaryDirectory, temporaryZip, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(temporaryZip, zipPath, overwrite: true);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
            if (File.Exists(temporaryZip)) File.Delete(temporaryZip);
        }
    }

    internal static string BuildFilter(Segment segment, int sampleRate)
    {
        var start = segment.StartTime.TotalSeconds;
        var duration = segment.Duration.TotalSeconds;
        var deleted = Normalize(segment.DeletedRanges, segment.StartTime, segment.EndTime)
            .Select(range => (Start: range.StartTime.TotalSeconds - start, End: range.EndTime.TotalSeconds - start)).ToArray();
        var muted = Normalize(segment.MutedRanges, segment.StartTime, segment.EndTime)
            .Select(range => (Start: range.StartTime.TotalSeconds - start, End: range.EndTime.TotalSeconds - start)).ToArray();
        var builder = new StringBuilder()
            .Append("[0:a:0]atrim=start=").Append(F(start)).Append(":end=").Append(F(segment.EndTime.TotalSeconds))
            .Append(",asetpts=PTS-STARTPTS");
        foreach (var range in muted)
        {
            builder.Append(",volume=volume=0:enable='between(t\\,").Append(F(range.Start)).Append("\\,").Append(F(range.End)).Append(")'");
        }

        if (deleted.Length == 0)
        {
            builder.Append(",aresample=").Append(sampleRate).Append("[out]");
            return builder.ToString();
        }

        var kept = new List<(double Start, double End)>();
        var cursor = 0d;
        foreach (var range in deleted)
        {
            if (range.Start > cursor) kept.Add((cursor, range.Start));
            cursor = Math.Max(cursor, range.End);
        }
        if (cursor < duration) kept.Add((cursor, duration));
        if (kept.Count == 0) throw new InvalidOperationException($"片段“{segment.Name}”的全部内容都被标记为删除。");

        builder.Append("[base];[base]asplit=").Append(kept.Count);
        for (var index = 0; index < kept.Count; index++) builder.Append("[s").Append(index).Append(']');
        builder.Append(';');
        for (var index = 0; index < kept.Count; index++)
        {
            builder.Append("[s").Append(index).Append("]atrim=start=").Append(F(kept[index].Start)).Append(":end=").Append(F(kept[index].End)).Append(",asetpts=PTS-STARTPTS[p").Append(index).Append("]; ");
        }
        if (kept.Count == 1) builder.Append("[p0]aresample=").Append(sampleRate).Append("[out]");
        else
        {
            var previous = "p0";
            for (var index = 1; index < kept.Count; index++)
            {
                var fade = Math.Min(0.003, Math.Min(kept[index - 1].End - kept[index - 1].Start, kept[index].End - kept[index].Start) / 2);
                var output = index == kept.Count - 1 ? "joined" : $"x{index}";
                builder.Append('[').Append(previous).Append("][p").Append(index).Append("]acrossfade=d=").Append(F(Math.Max(0.0001, fade))).Append(":c1=tri:c2=tri[").Append(output).Append("]; ");
                previous = output;
            }
            builder.Append('[').Append(previous).Append("]aresample=").Append(sampleRate).Append("[out]");
        }
        return builder.ToString();
    }

    private static IReadOnlyList<AudioRange> Normalize(IEnumerable<AudioRange> ranges, TimeSpan lower, TimeSpan upper)
    {
        var sorted = ranges.Select(range => new AudioRange(range.StartTime < lower ? lower : range.StartTime, range.EndTime > upper ? upper : range.EndTime)).Where(range => range.IsValid).OrderBy(range => range.StartTime).ToArray();
        var result = new List<AudioRange>();
        foreach (var range in sorted)
        {
            if (result.Count == 0 || range.StartTime > result[^1].EndTime) result.Add(range);
            else result[^1] = new AudioRange(result[^1].StartTime, range.EndTime > result[^1].EndTime ? range.EndTime : result[^1].EndTime);
        }
        return result;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "segment" : sanitized;
    }

    private static string FindAvailablePath(string directory, string baseName, string extension)
    {
        var path = Path.Combine(directory, $"{baseName}.{extension}");
        for (var suffix = 2; File.Exists(path); suffix++) path = Path.Combine(directory, $"{baseName}_{suffix}.{extension}");
        return path;
    }

    private static string F(double value) => value.ToString("0.#########", CultureInfo.InvariantCulture);
}
