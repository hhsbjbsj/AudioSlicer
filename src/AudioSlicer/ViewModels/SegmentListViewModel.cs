using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Models;

namespace AudioSlicer.ViewModels;

public sealed class SegmentListViewModel : ObservableObject
{
    public ObservableCollection<Segment> Segments { get; } = [];
}

