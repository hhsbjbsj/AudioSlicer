using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AudioSlicer.Media;
using AudioSlicer.Models;
using AudioSlicer.Editing;
using AudioSlicer.Services;
using AudioSlicer.Waveform;
using AudioSlicer.Project;
using AudioSlicer.Export;

namespace AudioSlicer.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IFileDialogService _fileDialogService;
    private readonly FFprobeService _ffprobeService;
    private readonly WaveformGenerator _waveformGenerator;
    private readonly ProjectSerializer _projectSerializer;
    private readonly AudioExporter _audioExporter;
    private readonly UndoManager<EditorSnapshot> _undoManager = new();
    private string _statusMessage = "请选择视频文件开始工作";
    private string _mediaFileName = "尚未导入媒体";
    private string _mediaPath = string.Empty;
    private string _mediaHash = string.Empty;
    private bool _isBusy;
    private double _operationProgress;
    private MediaInfo? _mediaInfo;
    private string _projectPath = string.Empty;

    public MainViewModel(
        IVideoPlayerService playerService,
        IFileDialogService fileDialogService,
        FFprobeService ffprobeService,
        WaveformGenerator waveformGenerator,
        ProjectSerializer projectSerializer,
        AudioExporter audioExporter)
    {
        _fileDialogService = fileDialogService;
        _ffprobeService = ffprobeService;
        _waveformGenerator = waveformGenerator;
        _projectSerializer = projectSerializer;
        _audioExporter = audioExporter;
        Player = new PlayerViewModel(playerService);
        Waveform = new WaveformViewModel();
        Segments = new SegmentListViewModel();
        Export = new ExportViewModel();
    }

    public PlayerViewModel Player { get; }
    public WaveformViewModel Waveform { get; }
    public SegmentListViewModel Segments { get; }
    public ExportViewModel Export { get; }
    public string MediaPath { get => _mediaPath; private set => SetProperty(ref _mediaPath, value); }
    public string MediaHash { get => _mediaHash; private set => SetProperty(ref _mediaHash, value); }
    public MediaInfo? MediaInfo { get => _mediaInfo; private set => SetProperty(ref _mediaInfo, value); }
    public string ProjectPath { get => _projectPath; private set => SetProperty(ref _projectPath, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string MediaFileName { get => _mediaFileName; private set => SetProperty(ref _mediaFileName, value); }
    public double OperationProgress { get => _operationProgress; private set => SetProperty(ref _operationProgress, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) OpenMediaCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenMedia), IncludeCancelCommand = true)]
    private async Task OpenMediaAsync(CancellationToken cancellationToken)
    {
        var filePath = _fileDialogService.SelectMediaFile();
        if (!string.IsNullOrWhiteSpace(filePath)) await LoadMediaAsync(filePath, cancellationToken);
    }

    public async Task LoadMediaAsync(string filePath, CancellationToken cancellationToken)
    {
        IsBusy = true;
        OperationProgress = 0;
        StatusMessage = "正在分析媒体…";
        try
        {
            var info = await _ffprobeService.ProbeAsync(filePath, cancellationToken);
            if (!info.HasAudio) throw new InvalidDataException("该视频不包含可用音频轨道。");

            MediaInfo = info;
            StatusMessage = "正在生成多级波形缓存…";
            var progress = new Progress<double>(value => OperationProgress = value);
            var playerTask = Player.LoadAsync(filePath, cancellationToken);
            var waveformTask = _waveformGenerator.GenerateAsync(filePath, info.Duration, progress, cancellationToken);
            await playerTask;
            var result = await waveformTask;
            Waveform.Load(result.Waveform);
            MediaPath = filePath;
            MediaHash = result.MediaHash;
            MediaFileName = Path.GetFileName(filePath);
            Segments.ReplaceAll([]);
            _undoManager.Clear();
            StatusMessage = result.FromCache ? "媒体已就绪（已读取波形缓存）" : "媒体已就绪";
            AppLogger.Info($"已导入媒体：{filePath}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "操作已取消";
        }
        catch (Exception exception)
        {
            StatusMessage = $"导入失败：{exception.Message}";
            AppLogger.Error("导入媒体失败", exception);
        }
        finally
        {
            IsBusy = false;
            OperationProgress = 0;
        }
    }

    [RelayCommand]
    private void ApplySelectionTimes()
    {
        StatusMessage = Waveform.ApplyTextSelection(out var error) ? "选区时间已更新" : error!;
    }

    [RelayCommand]
    private void SetSelectionStart() => Waveform.SetSelection(Player.CurrentTimeSeconds, Math.Max(Player.CurrentTimeSeconds, Waveform.SelectionEndSeconds));

    [RelayCommand]
    private void SetSelectionEnd() => Waveform.SetSelection(Math.Min(Waveform.SelectionStartSeconds, Player.CurrentTimeSeconds), Player.CurrentTimeSeconds);

    [RelayCommand]
    private void ClearSelection()
    {
        Waveform.ClearSelection();
        Player.ConfigureLoop(0, 0, false);
    }

    [RelayCommand]
    private void ToggleLoop()
    {
        if (!Waveform.HasSelection)
        {
            StatusMessage = "请先在波形上选择一个区间。";
            return;
        }
        Player.ConfigureLoop(Waveform.SelectionStartSeconds, Waveform.SelectionEndSeconds, !Player.IsLooping);
        StatusMessage = Player.IsLooping ? "正在循环播放选区" : "已关闭循环播放";
    }

    [RelayCommand] private void StepBackward() => Player.Step(-100);
    [RelayCommand] private void StepForward() => Player.Step(100);
    [RelayCommand] private void FineStepBackward() => Player.Step(-1);
    [RelayCommand] private void FineStepForward() => Player.Step(1);
    [RelayCommand] private void GoSelectionStart() => Player.CurrentTimeSeconds = Waveform.SelectionStartSeconds;

    [RelayCommand]
    private void AddSegment()
    {
        if (!Waveform.HasSelection) { StatusMessage = "请先在波形上选择音频区间。"; return; }
        RecordMutation(() =>
        {
            var item = Segments.Add(new Segment
            {
                Name = $"片段 {Segments.Segments.Count + 1:000}",
                StartTime = TimeSpan.FromSeconds(Waveform.SelectionStartSeconds),
                EndTime = TimeSpan.FromSeconds(Waveform.SelectionEndSeconds),
            });
            Segments.ActiveSegment = item;
            Segments.SortByTime();
        });
        StatusMessage = "已添加非破坏性片段";
    }

    [RelayCommand]
    private void ActivateSegment(SegmentItemViewModel? item)
    {
        if (item is null) return;
        Segments.ActiveSegment = item;
        Waveform.SetSelection(item.StartTime.TotalSeconds, item.EndTime.TotalSeconds);
        Player.CurrentTimeSeconds = item.StartTime.TotalSeconds;
        var padding = Math.Max(0.5, item.Model.Duration.TotalSeconds * 0.5);
        Waveform.ViewportDurationSeconds = Math.Min(Waveform.DurationSeconds, item.Model.Duration.TotalSeconds + padding * 2);
        Waveform.ViewportStartSeconds = Math.Max(0, item.StartTime.TotalSeconds - padding);
        StatusMessage = $"正在编辑：{item.Name}";
    }

    [RelayCommand]
    private void UpdateActiveSegmentCuts()
    {
        var item = Segments.ActiveSegment;
        if (item is null || !Waveform.HasSelection) { StatusMessage = "请先双击片段并选择新的边界。"; return; }
        RecordMutation(() =>
        {
            item.StartTime = TimeSpan.FromSeconds(Waveform.SelectionStartSeconds);
            item.EndTime = TimeSpan.FromSeconds(Waveform.SelectionEndSeconds);
            item.Model.DeletedRanges.RemoveAll(range => range.StartTime < item.StartTime || range.EndTime > item.EndTime);
            item.Model.MutedRanges.RemoveAll(range => range.StartTime < item.StartTime || range.EndTime > item.EndTime);
            item.Refresh();
            Segments.SortByTime();
        });
        StatusMessage = "片段切点已更新";
    }

    [RelayCommand] private void DeleteRange() => AddInternalRange(deleted: true);
    [RelayCommand] private void MuteRange() => AddInternalRange(deleted: false);

    private void AddInternalRange(bool deleted)
    {
        var item = Segments.ActiveSegment;
        if (item is null || !Waveform.HasSelection) { StatusMessage = "请先双击片段，再框选片段内部的小区间。"; return; }
        var range = new AudioRange(TimeSpan.FromSeconds(Waveform.SelectionStartSeconds), TimeSpan.FromSeconds(Waveform.SelectionEndSeconds));
        if (range.StartTime < item.StartTime || range.EndTime > item.EndTime) { StatusMessage = "内部编辑区间不能超出当前片段。"; return; }
        RecordMutation(() =>
        {
            (deleted ? item.Model.DeletedRanges : item.Model.MutedRanges).Add(range);
            item.Refresh();
        });
        StatusMessage = deleted ? "已记录删除区间（原媒体未修改）" : "已记录静音区间（原媒体未修改）";
    }

    [RelayCommand]
    private void DeleteSelectedSegments()
    {
        if (!Segments.SelectedItems.Any()) return;
        RecordMutation(Segments.RemoveSelected);
        StatusMessage = "已删除所选片段";
    }

    [RelayCommand] private void SelectAllSegments() => Segments.SelectAll();
    [RelayCommand] private void SortSegments() => Segments.SortByTime();

    [RelayCommand]
    private void PreviewActiveSegment()
    {
        var item = Segments.ActiveSegment;
        if (item is null) { StatusMessage = "请先双击一个片段。"; return; }
        Waveform.SetSelection(item.StartTime.TotalSeconds, item.EndTime.TotalSeconds);
        Player.ConfigureLoop(item.StartTime.TotalSeconds, item.EndTime.TotalSeconds, true);
        StatusMessage = $"正在循环试听：{item.Name}";
    }

    [RelayCommand]
    private void ClearActiveSegmentEdits()
    {
        var item = Segments.ActiveSegment;
        if (item is null) return;
        RecordMutation(() => { item.Model.DeletedRanges.Clear(); item.Model.MutedRanges.Clear(); item.Refresh(); });
        StatusMessage = "已清除当前片段的内部删除和静音标记";
    }

    public void SetSelectedSegments(IEnumerable<SegmentItemViewModel> selected) => Segments.SetSelected(selected);

    public void CommitRename(SegmentItemViewModel item, string oldName)
    {
        var newName = item.Name.Trim();
        if (newName == oldName) return;
        item.Name = oldName;
        var before = CaptureSnapshot();
        item.Name = string.IsNullOrWhiteSpace(newName) ? $"片段 {item.Number:000}" : newName;
        var after = CaptureSnapshot();
        _undoManager.Record(before, after);
        RefreshUndoCommands();
        StatusMessage = "片段已重命名";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        RestoreSnapshot(_undoManager.Undo(CaptureSnapshot()));
        RefreshUndoCommands();
        StatusMessage = "已撤销";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        RestoreSnapshot(_undoManager.Redo(CaptureSnapshot()));
        RefreshUndoCommands();
        StatusMessage = "已重做";
    }

    private bool CanUndo() => _undoManager.CanUndo;
    private bool CanRedo() => _undoManager.CanRedo;

    [RelayCommand]
    private async Task SaveProjectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(MediaPath)) { StatusMessage = "请先导入媒体。"; return; }
        var path = string.IsNullOrWhiteSpace(ProjectPath)
            ? _fileDialogService.SelectProjectToSave(Path.GetFileNameWithoutExtension(MediaPath))
            : ProjectPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            await _projectSerializer.SaveAsync(path, CreateProject(), cancellationToken);
            ProjectPath = path;
            StatusMessage = "工程已保存";
        }
        catch (Exception exception) { AppLogger.Error("保存工程失败", exception); StatusMessage = $"保存失败：{exception.Message}"; }
    }

    [RelayCommand]
    private async Task SaveProjectAsAsync(CancellationToken cancellationToken)
    {
        ProjectPath = string.Empty;
        await SaveProjectAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task OpenProjectAsync(CancellationToken cancellationToken)
    {
        var path = _fileDialogService.SelectProjectToOpen();
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var project = await _projectSerializer.LoadAsync(path, cancellationToken);
            var mediaPath = project.VideoPath;
            if (!File.Exists(mediaPath))
            {
                StatusMessage = "工程引用的视频已移动，请重新定位原视频。";
                mediaPath = _fileDialogService.SelectMediaFile();
                if (string.IsNullOrWhiteSpace(mediaPath)) return;
            }
            await LoadMediaAsync(mediaPath, cancellationToken);
            if (!string.Equals(MediaPath, mediaPath, StringComparison.OrdinalIgnoreCase)) return;
            if (!string.IsNullOrWhiteSpace(project.VideoHash) && !string.Equals(project.VideoHash, MediaHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("视频文件与工程记录的哈希不一致。");
            Segments.ReplaceAll(project.Segments.Select(segment => segment.Clone()));
            CopyExportSettings(project.ExportSettings, Export.Settings);
            Export.Refresh();
            Waveform.ViewportDurationSeconds = Math.Clamp(project.UIState.ViewportDurationSeconds, 0.01, Math.Max(0.01, Waveform.DurationSeconds));
            Waveform.ViewportStartSeconds = project.UIState.ViewportStartSeconds;
            Player.CurrentTimeSeconds = project.UIState.CurrentTimeSeconds;
            ProjectPath = path;
            _undoManager.Clear(); RefreshUndoCommands();
            StatusMessage = "工程已恢复";
        }
        catch (Exception exception) { AppLogger.Error("打开工程失败", exception); StatusMessage = $"打开工程失败：{exception.Message}"; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExportSelectedAsync(CancellationToken cancellationToken)
    {
        var directory = _fileDialogService.SelectExportDirectory();
        if (!string.IsNullOrWhiteSpace(directory)) await RunExportAsync(directory, zip: false, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExportZipAsync(CancellationToken cancellationToken)
    {
        var path = _fileDialogService.SelectZipPath($"{Path.GetFileNameWithoutExtension(MediaPath)}_audio_segments.zip");
        if (!string.IsNullOrWhiteSpace(path)) await RunExportAsync(path, zip: true, cancellationToken);
    }

    private async Task RunExportAsync(string target, bool zip, CancellationToken cancellationToken)
    {
        var items = Segments.ExportItems.Select(item => item.Model.Clone()).ToArray();
        if (items.Length == 0 || string.IsNullOrWhiteSpace(MediaPath)) { StatusMessage = "没有可导出的片段。"; return; }
        IsBusy = true; OperationProgress = 0; StatusMessage = zip ? "正在导出并打包…" : "正在批量导出…";
        try
        {
            var progress = new Progress<double>(value => OperationProgress = value);
            if (zip) await _audioExporter.ExportZipAsync(MediaPath, items, target, Export.Settings, progress, cancellationToken);
            else await _audioExporter.ExportBatchAsync(MediaPath, items, target, Export.Settings, progress, cancellationToken);
            StatusMessage = zip ? "ZIP 打包完成" : $"已导出 {items.Length} 个片段";
        }
        catch (OperationCanceledException) { StatusMessage = "导出已取消"; }
        catch (Exception exception) { AppLogger.Error("导出失败", exception); StatusMessage = $"导出失败：{exception.Message}"; }
        finally { IsBusy = false; OperationProgress = 0; }
    }

    private void RecordMutation(Action mutation)
    {
        var before = CaptureSnapshot(); mutation(); var after = CaptureSnapshot();
        _undoManager.Record(before, after); RefreshUndoCommands();
    }

    private EditorSnapshot CaptureSnapshot() => new(Segments.Segments.Select(item => item.Model.Clone()).ToList(), Segments.ActiveSegment?.Id);
    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        Segments.ReplaceAll(snapshot.Segments.Select(segment => segment.Clone()));
        var active = Segments.Segments.FirstOrDefault(item => item.Id == snapshot.ActiveId);
        if (active is not null) ActivateSegment(active);
    }
    private void RefreshUndoCommands() { UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged(); }

    private AudioSliceProject CreateProject() => new()
    {
        VideoPath = MediaPath,
        VideoHash = MediaHash,
        Segments = Segments.Segments.Select(item => item.Model.Clone()).ToList(),
        ExportSettings = CloneExportSettings(Export.Settings),
        UIState = new ProjectUiState { ViewportStartSeconds = Waveform.ViewportStartSeconds, ViewportDurationSeconds = Waveform.ViewportDurationSeconds, CurrentTimeSeconds = Player.CurrentTimeSeconds },
    };
    private static ExportSettings CloneExportSettings(ExportSettings source) => new() { Format = source.Format, SampleRate = source.SampleRate, Channels = source.Channels, BitsPerSample = source.BitsPerSample, Mp3BitRateKbps = source.Mp3BitRateKbps };
    private static void CopyExportSettings(ExportSettings source, ExportSettings target) { target.Format = source.Format; target.SampleRate = source.SampleRate; target.Channels = source.Channels; target.BitsPerSample = source.BitsPerSample; target.Mp3BitRateKbps = source.Mp3BitRateKbps; }

    private sealed record EditorSnapshot(List<Segment> Segments, Guid? ActiveId);

    private bool CanOpenMedia() => !IsBusy;
    public void Dispose() => Player.Dispose();
}
