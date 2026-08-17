using AudioSlicer.Models;
using Xunit;

namespace AudioSlicer.Tests;

public sealed class AudioRangeTests
{
    [Fact]
    public void FiftyFiveMillisecondRange_RemainsValidAndExact()
    {
        var range = new AudioRange(
            TimeSpan.FromSeconds(10.125),
            TimeSpan.FromSeconds(10.180));

        Assert.True(range.IsValid);
        Assert.Equal(TimeSpan.FromMilliseconds(55), range.Duration);
    }
}
