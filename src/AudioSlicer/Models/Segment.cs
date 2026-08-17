namespace AudioSlicer.Models;

public sealed class Segment
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public List<AudioRange> DeletedRanges { get; init; } = [];

    public List<AudioRange> MutedRanges { get; init; } = [];

    public TimeSpan Duration => EndTime - StartTime;
}

