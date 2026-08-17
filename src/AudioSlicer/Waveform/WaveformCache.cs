using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AudioSlicer.Waveform;

public sealed class WaveformCache
{
    private const int CacheVersion = 1;
    private readonly string _cacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSlicer",
        "cache");

    public async Task<string> ComputeMediaHashAsync(string mediaPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            mediaPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<WaveformData?> TryLoadAsync(
        string mediaHash,
        FileInfo sourceFile,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_cacheRoot, mediaHash);
        var metadataPath = Path.Combine(directory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        await using var metadataStream = File.OpenRead(metadataPath);
        var metadata = await JsonSerializer.DeserializeAsync<CacheMetadata>(metadataStream, cancellationToken: cancellationToken);
        if (metadata is null || metadata.CacheVersion != CacheVersion || metadata.SourceLength != sourceFile.Length)
        {
            return null;
        }

        var levels = new List<WaveformLevel>(metadata.Levels.Count);
        foreach (var descriptor in metadata.Levels)
        {
            var levelPath = Path.Combine(directory, descriptor.FileName);
            if (!File.Exists(levelPath))
            {
                return null;
            }

            var peaks = new PeakPair[descriptor.PeakCount];
            await using var stream = new FileStream(levelPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            using var reader = new BinaryReader(stream);
            for (var index = 0; index < peaks.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                peaks[index] = new PeakPair(reader.ReadSingle(), reader.ReadSingle());
            }

            levels.Add(new WaveformLevel(descriptor.SamplesPerPeak, peaks));
        }

        return new WaveformData(
            metadata.SampleRate,
            metadata.TotalSamples,
            TimeSpan.FromTicks(metadata.DurationTicks),
            levels);
    }

    public async Task SaveAsync(
        string mediaHash,
        FileInfo sourceFile,
        WaveformData waveform,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_cacheRoot, mediaHash);
        Directory.CreateDirectory(directory);
        var descriptors = new List<LevelDescriptor>(waveform.Levels.Count);

        for (var levelIndex = 0; levelIndex < waveform.Levels.Count; levelIndex++)
        {
            var level = waveform.Levels[levelIndex];
            var fileName = $"waveform-{levelIndex}.dat";
            var finalPath = Path.Combine(directory, fileName);
            var temporaryPath = finalPath + ".tmp";
            var bytes = new byte[level.Peaks.Length * sizeof(float) * 2];
            for (var peakIndex = 0; peakIndex < level.Peaks.Length; peakIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var offset = peakIndex * sizeof(float) * 2;
                BitConverter.TryWriteBytes(bytes.AsSpan(offset, sizeof(float)), level.Peaks[peakIndex].Minimum);
                BitConverter.TryWriteBytes(bytes.AsSpan(offset + sizeof(float), sizeof(float)), level.Peaks[peakIndex].Maximum);
            }
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);

            File.Move(temporaryPath, finalPath, overwrite: true);
            descriptors.Add(new LevelDescriptor(fileName, level.SamplesPerPeak, level.Peaks.Length));
        }

        var metadata = new CacheMetadata(
            CacheVersion,
            sourceFile.Length,
            sourceFile.LastWriteTimeUtc.Ticks,
            waveform.SampleRate,
            waveform.TotalSamples,
            waveform.Duration.Ticks,
            descriptors);
        var metadataPath = Path.Combine(directory, "metadata.json");
        var metadataTemporaryPath = metadataPath + ".tmp";
        await File.WriteAllTextAsync(
            metadataTemporaryPath,
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        File.Move(metadataTemporaryPath, metadataPath, overwrite: true);
    }

    private sealed record CacheMetadata(
        int CacheVersion,
        long SourceLength,
        long SourceWriteTimeUtcTicks,
        int SampleRate,
        long TotalSamples,
        long DurationTicks,
        List<LevelDescriptor> Levels);

    private sealed record LevelDescriptor(string FileName, int SamplesPerPeak, int PeakCount);

}
