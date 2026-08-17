using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Models;

namespace AudioSlicer.ViewModels;

public sealed class ExportViewModel : ObservableObject
{
    public ExportSettings Settings { get; } = new();
    public IReadOnlyList<AudioExportFormat> Formats { get; } = Enum.GetValues<AudioExportFormat>();
    public IReadOnlyList<int> SampleRates { get; } = [48_000, 44_100, 32_000, 24_000, 22_050, 16_000];
    public IReadOnlyList<int> ChannelOptions { get; } = [1, 2];
    public AudioExportFormat Format { get => Settings.Format; set { if (Settings.Format != value) { Settings.Format = value; OnPropertyChanged(); } } }
    public int SampleRate { get => Settings.SampleRate; set { if (Settings.SampleRate != value) { Settings.SampleRate = value; OnPropertyChanged(); } } }
    public int Channels { get => Settings.Channels; set { if (Settings.Channels != value) { Settings.Channels = value; OnPropertyChanged(); } } }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Format));
        OnPropertyChanged(nameof(SampleRate));
        OnPropertyChanged(nameof(Channels));
    }
}
