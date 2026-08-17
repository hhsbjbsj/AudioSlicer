using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Models;

namespace AudioSlicer.ViewModels;

public sealed class SegmentItemViewModel : ObservableObject
{
    private bool _isExportSelected = true;
    private int _number;
    private bool _isListSelected;
    private string _audioFilePath = string.Empty;
    private string _fileStatus = "等待生成音频";
    private bool _isRendering;

    public SegmentItemViewModel(Segment model) => Model = model;
    public Segment Model { get; }
    public Guid Id => Model.Id;

    public int Number
    {
        get => _number;
        set { if (SetProperty(ref _number, value)) OnPropertyChanged(nameof(NumberText)); }
    }
    public string Name { get => Model.Name; set { if (Model.Name != value) { Model.Name = value; OnPropertyChanged(); } } }
    public TimeSpan StartTime { get => Model.StartTime; set { Model.StartTime = value; Refresh(); } }
    public TimeSpan EndTime { get => Model.EndTime; set { Model.EndTime = value; Refresh(); } }
    public bool IsExportSelected { get => _isExportSelected; set => SetProperty(ref _isExportSelected, value); }
    public bool IsListSelected { get => _isListSelected; set => SetProperty(ref _isListSelected, value); }
    public string AudioFilePath { get => _audioFilePath; set { if (SetProperty(ref _audioFilePath, value)) OnPropertyChanged(nameof(HasAudioFile)); } }
    public string FileStatus { get => _fileStatus; set => SetProperty(ref _fileStatus, value); }
    public bool IsRendering { get => _isRendering; set => SetProperty(ref _isRendering, value); }
    public bool HasAudioFile => !string.IsNullOrWhiteSpace(AudioFilePath) && File.Exists(AudioFilePath);
    public string NumberText => Number.ToString("000");
    public string RangeText => $"{WaveformViewModel.FormatDuration(StartTime.TotalSeconds)} → {WaveformViewModel.FormatDuration(EndTime.TotalSeconds)}";
    public string DurationText => $"{Model.Duration.TotalSeconds:0.000} 秒";
    public string EditSummary => $"删除 {Model.DeletedRanges.Count} · 静音 {Model.MutedRanges.Count}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(RangeText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(EditSummary));
    }
}
