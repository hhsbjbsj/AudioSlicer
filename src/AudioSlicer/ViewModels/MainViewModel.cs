using System.IO;
using System.IO.Compression;
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
    private readonly AudioPreviewService _audioPreviewService;
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
        AudioExporter audioExporter,
        AudioPreviewService audioPreviewService)
    {
        _fileDialogService = fileDialogService;
        _ffprobeService = ffprobeService;
        _waveformGenerator = waveformGenerator;
        _projectSerializer = projectSerializer;
        _audioExporter = audioExporter;
        _audioPreviewService = audioPreviewService;
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
    private async Task AddSegmentAsync(CancellationToken cancellationToken)
    {
        if (!Waveform.HasSelection) { StatusMessage = "请先在波形上选择音频区间。"; return; }
        SegmentItemViewModel? item = null;
        RecordMutation(() =>
        {
            item = Segments.Add(new Segment
            {
                Name = $"片段 {Segments.Segments.Count + 1:000}",
                StartTime = TimeSpan.FromSeconds(Waveform.SelectionStartSeconds),
                EndTime = TimeSpan.FromSeconds(Waveform.SelectionEndSeconds),
            });
            Segments.ActiveSegment = item;
            Segments.SortByTime();
        });
        StatusMessage = "正在生成独立任务音频…";
        await RenderTaskAudioAsync(item!, cancellationToken);
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
    private async Task UpdateActiveSegmentCutsAsync(CancellationToken cancellationToken)
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
        await RenderTaskAudioAsync(item, cancellationToken);
        StatusMessage = "片段切点和独立音频文件已更新";
    }

    [RelayCommand] private Task DeleteRangeAsync(CancellationToken cancellationToken) => AddInternalRangeAsync(deleted: true, cancellationToken);
    [RelayCommand] private Task MuteRangeAsync(CancellationToken cancellationToken) => AddInternalRangeAsync(deleted: false, cancellationToken);

    private async Task AddInternalRangeAsync(bool deleted, CancellationToken cancellationToken)
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
        await RenderTaskAudioAsync(item, cancellationToken);
        StatusMessage = deleted ? "删除区间已应用到独立音频文件" : "静音区间已应用到独立音频文件";
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
    private async Task ClearActiveSegmentEditsAsync(CancellationToken cancellationToken)
    {
        var item = Segments.ActiveSegment;
        if (item is null) return;
        RecordMutation(() => { item.Model.DeletedRanges.Clear(); item.Model.MutedRanges.Clear(); item.Refresh(); });
        await RenderTaskAudioAsync(item, cancellationToken);
        StatusMessage = "已清除内部编辑并更新独立音频文件";
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
    private async Task UndoAsync()
    {
        RestoreSnapshot(_undoManager.Undo(CaptureSnapshot()));
        await RefreshAllTaskAudioAsync(CancellationToken.None);
        RefreshUndoCommands();
        StatusMessage = "已撤销";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private async Task RedoAsync()
    {
        RestoreSnapshot(_undoManager.Redo(CaptureSnapshot()));
        await RefreshAllTaskAudioAsync(CancellationToken.None);
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
            await RefreshAllTaskAudioAsync(cancellationToken);
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

    [RelayCommand]
    private void PlayTaskAudio(SegmentItemViewModel? item)
    {
        if (item is null || !item.HasAudioFile)
        {
            StatusMessage = "该任务的独立音频尚未生成。";
            return;
        }

        try
        {
            Player.PausePlayback();
            _audioPreviewService.Play(item.AudioFilePath);
            StatusMessage = $"正在播放独立音频：{item.Name}";
        }
        catch (Exception exception)
        {
            AppLogger.Error("播放任务音频失败", exception);
            StatusMessage = $"播放失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RegenerateTaskAudioAsync(SegmentItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is not null) await RenderTaskAudioAsync(item, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PackageTaskAudioAsync(CancellationToken cancellationToken)
    {
        var items = Segments.ExportItems.ToArray();
        if (items.Length == 0) { StatusMessage = "请先勾选要打包的任务音频。"; return; }
        var zipPath = _fileDialogService.SelectZipPath($"{Path.GetFileNameWithoutExtension(MediaPath)}_任务音频.zip");
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        IsBusy = true;
        OperationProgress = 0;
        StatusMessage = "正在准备并打包任务音频…";
        var temporaryZip = zipPath + ".partial";
        try
        {
            for (var index = 0; index < items.Length; index++)
            {
                if (!items[index].HasAudioFile) await RenderTaskAudioAsync(items[index], cancellationToken);
                if (!items[index].HasAudioFile) throw new InvalidOperationException($"任务“{items[index].Name}”的音频文件生成失败。");
                OperationProgress = (index + 0.5) / items.Length;
            }

            if (File.Exists(temporaryZip)) File.Delete(temporaryZip);
            await using (var zipStream = new FileStream(temporaryZip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, true))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < items.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var baseName = SanitizeTaskFileName(items[index].Name);
                    var entryName = $"{baseName}.wav";
                    for (var suffix = 2; !usedNames.Add(entryName); suffix++) entryName = $"{baseName}_{suffix}.wav";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var source = new FileStream(items[index].AudioFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                    await using var destination = entry.Open();
                    await source.CopyToAsync(destination, cancellationToken);
                    OperationProgress = (index + 1d) / items.Length;
                }
            }
            File.Move(temporaryZip, zipPath, overwrite: true);
            StatusMessage = $"已把 {items.Length} 个独立音频文件打包完成";
        }
        catch (OperationCanceledException) { StatusMessage = "打包已取消"; }
        catch (Exception exception) { AppLogger.Error("任务音频打包失败", exception); StatusMessage = $"打包失败：{exception.Message}"; }
        finally
        {
            if (File.Exists(temporaryZip)) File.Delete(temporaryZip);
            IsBusy = false;
            OperationProgress = 0;
        }
    }

    private async Task<bool> RenderTaskAudioAsync(SegmentItemViewModel item, CancellationToken cancellationToken)
    {
        item.IsRendering = true;
        item.FileStatus = "正在生成独立 WAV…";
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AudioSlicer",
                "task-audio",
                string.IsNullOrWhiteSpace(MediaHash) ? "unsaved" : MediaHash);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{item.Id:N}.wav");
            await _audioExporter.ExportOneAsync(
                MediaPath,
                item.Model.Clone(),
                path,
                new ExportSettings { Format = AudioExportFormat.Wav, SampleRate = 48_000, Channels = 1, BitsPerSample = 16 },
                cancellationToken);
            item.AudioFilePath = path;
            item.FileStatus = $"独立 WAV 已生成 · {new FileInfo(path).Length / 1024d:0.#} KB";
            StatusMessage = $"已生成音频并加入任务栏：{item.Name}";
            return true;
        }
        catch (OperationCanceledException)
        {
            item.FileStatus = "生成已取消";
            return false;
        }
        catch (Exception exception)
        {
            item.AudioFilePath = string.Empty;
            item.FileStatus = $"生成失败：{exception.Message}";
            AppLogger.Error($"生成任务音频失败：{item.Name}", exception);
            StatusMessage = item.FileStatus;
            return false;
        }
        finally
        {
            item.IsRendering = false;
        }
    }

    private async Task RefreshAllTaskAudioAsync(CancellationToken cancellationToken)
    {
        foreach (var item in Segments.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RenderTaskAudioAsync(item, cancellationToken);
        }
    }

    private static string SanitizeTaskFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(value) ? "audio_segment" : value;
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
    public void Dispose()
    {
        _audioPreviewService.Dispose();
        Player.Dispose();
    }
}
