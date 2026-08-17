using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioSlicer.ViewModels;

public sealed class WaveformViewModel : ObservableObject
{
    public string PhaseMessage => "音频波形将在 Phase 2 接入统一时间轴";
}

