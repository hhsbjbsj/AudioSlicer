using System.IO;

namespace AudioSlicer.Media;

public sealed class FFmpegLocator
{
    public FFmpegLocator()
    {
        FFmpegPath = Resolve("ffmpeg.exe");
        FFprobePath = Resolve("ffprobe.exe");
    }

    public string FFmpegPath { get; }

    public string FFprobePath { get; }

    private static string Resolve(string fileName)
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("AUDIO_SLICER_FFMPEG_DIR");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var configuredPath = Path.Combine(configuredDirectory, fileName);
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tools", "ffmpeg", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"找不到 {fileName}。请重新安装 Audio Slicer，或通过 AUDIO_SLICER_FFMPEG_DIR 指定工具目录。");
    }
}

