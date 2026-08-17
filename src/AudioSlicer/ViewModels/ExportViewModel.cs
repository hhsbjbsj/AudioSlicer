using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Models;

namespace AudioSlicer.ViewModels;

public sealed class ExportViewModel : ObservableObject
{
    public ExportSettings Settings { get; } = new();
}

