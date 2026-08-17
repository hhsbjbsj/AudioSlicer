using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AudioSlicer.Models;

namespace AudioSlicer.ViewModels;

public sealed class SegmentListViewModel : ObservableObject
{
    private readonly HashSet<Guid> _selectedIds = [];
    private SegmentItemViewModel? _activeSegment;

    public ObservableCollection<SegmentItemViewModel> Segments { get; } = [];
    public SegmentItemViewModel? ActiveSegment { get => _activeSegment; set => SetProperty(ref _activeSegment, value); }
    public IReadOnlyCollection<Guid> SelectedIds => _selectedIds;
    public IEnumerable<SegmentItemViewModel> SelectedItems => Segments.Where(item => _selectedIds.Contains(item.Id));
    public IEnumerable<SegmentItemViewModel> ExportItems => Segments.Where(item => item.IsExportSelected);

    public SegmentItemViewModel Add(Segment segment)
    {
        var item = new SegmentItemViewModel(segment);
        Segments.Add(item);
        Renumber();
        return item;
    }

    public void ReplaceAll(IEnumerable<Segment> segments)
    {
        Segments.Clear();
        foreach (var segment in segments.OrderBy(value => value.StartTime)) Segments.Add(new SegmentItemViewModel(segment));
        _selectedIds.Clear();
        ActiveSegment = null;
        Renumber();
    }

    public void SetSelected(IEnumerable<SegmentItemViewModel> selected)
    {
        _selectedIds.Clear();
        foreach (var item in selected) _selectedIds.Add(item.Id);
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void SelectAllForExport(bool selected)
    {
        foreach (var item in Segments) item.IsExportSelected = selected;
    }

    public void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var item in Segments) { item.IsListSelected = true; item.IsExportSelected = true; _selectedIds.Add(item.Id); }
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void RemoveSelected()
    {
        foreach (var item in SelectedItems.ToArray()) Segments.Remove(item);
        _selectedIds.Clear();
        if (ActiveSegment is not null && !Segments.Contains(ActiveSegment)) ActiveSegment = null;
        Renumber();
    }

    public void SortByTime()
    {
        var sorted = Segments.OrderBy(item => item.StartTime).ThenBy(item => item.EndTime).ToArray();
        Segments.Clear();
        foreach (var item in sorted) Segments.Add(item);
        Renumber();
    }

    private void Renumber()
    {
        for (var index = 0; index < Segments.Count; index++) Segments[index].Number = index + 1;
    }
}
