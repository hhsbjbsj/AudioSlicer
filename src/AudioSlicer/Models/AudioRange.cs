namespace AudioSlicer.Models;

public sealed record AudioRange(TimeSpan StartTime, TimeSpan EndTime)
{
    public TimeSpan Duration => EndTime - StartTime;

    public bool IsValid => StartTime >= TimeSpan.Zero && EndTime > StartTime;
}

