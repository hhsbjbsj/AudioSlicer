using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioSlicer.Models;

namespace AudioSlicer.Project;

public sealed class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task SaveAsync(string path, AudioSliceProject project, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, project, Options, cancellationToken);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task<AudioSliceProject> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<AudioSliceProject>(stream, Options, cancellationToken);
        if (project is null) throw new InvalidDataException("工程文件为空或格式无效。");
        if (project.ProjectVersion is < 1 or > 1) throw new InvalidDataException($"不支持的工程版本：{project.ProjectVersion}。");
        return project;
    }
}
