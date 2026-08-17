using System.Globalization;
using System.Text.Json;

namespace AudioSlicer.Media;

public sealed class FFprobeService(FFmpegLocator locator)
{
    public async Task<MediaInfo> ProbeAsync(string mediaPath, CancellationToken cancellationToken)
    {
        var json = await ProcessRunner.RunForTextAsync(
            locator.FFprobePath,
            ["-v", "error", "-show_format", "-show_streams", "-of", "json", mediaPath],
            cancellationToken);

        using var document = JsonDocument.Parse(json);
        var streams = document.RootElement.GetProperty("streams");
        JsonElement? audio = null;
        JsonElement? video = null;
        foreach (var stream in streams.EnumerateArray())
        {
            var type = GetString(stream, "codec_type");
            if (type == "audio" && audio is null)
            {
                audio = stream.Clone();
            }
            else if (type == "video" && video is null)
            {
                video = stream.Clone();
            }
        }

        var durationText = document.RootElement.GetProperty("format").TryGetProperty("duration", out var duration)
            ? duration.GetString()
            : null;
        _ = double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds);

        return new MediaInfo(
            TimeSpan.FromSeconds(Math.Max(0, durationSeconds)),
            video is not null,
            audio is not null,
            ParseInt(audio, "sample_rate"),
            GetInt(audio, "channels"),
            GetString(audio, "codec_name"),
            GetString(video, "codec_name"),
            GetInt(video, "width"),
            GetInt(video, "height"));
    }

    private static string GetString(JsonElement? element, string propertyName) =>
        element is { } value && value.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static int GetInt(JsonElement? element, string propertyName) =>
        element is { } value && value.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var result)
            ? result
            : 0;

    private static int ParseInt(JsonElement? element, string propertyName) =>
        int.TryParse(GetString(element, propertyName), CultureInfo.InvariantCulture, out var result) ? result : 0;
}
