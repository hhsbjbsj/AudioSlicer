namespace AudioSlicer.Models;

public sealed class AudioSliceProject
{
    public int ProjectVersion { get; set; } = 1;

    public string VideoPath { get; set; } = string.Empty;

    public string VideoHash { get; set; } = string.Empty;

    public List<Segment> Segments { get; init; } = [];

    public ExportSettings ExportSettings { get; init; } = new();

    public ProjectUiState UIState { get; init; } = new();
}

public sealed class ProjectUiState
{
    public double ViewportStartSeconds { get; set; }
    public double ViewportDurationSeconds { get; set; }
    public double CurrentTimeSeconds { get; set; }
}
