using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AudioSlicer.Media;
using AudioSlicer.Services;

namespace AudioSlicer.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IFileDialogService _fileDialogService;
    private string _statusMessage = "请选择视频文件开始工作";
    private string _mediaFileName = "尚未导入媒体";
    private bool _isBusy;

    public MainViewModel(
        IVideoPlayerService playerService,
        IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
        Player = new PlayerViewModel(playerService);
        Waveform = new WaveformViewModel();
        Segments = new SegmentListViewModel();
        Export = new ExportViewModel();
    }

    public PlayerViewModel Player { get; }

    public WaveformViewModel Waveform { get; }

    public SegmentListViewModel Segments { get; }

    public ExportViewModel Export { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string MediaFileName
    {
        get => _mediaFileName;
        private set => SetProperty(ref _mediaFileName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OpenMediaCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenMedia), IncludeCancelCommand = true)]
    private async Task OpenMediaAsync(CancellationToken cancellationToken)
    {
        var filePath = _fileDialogService.SelectMediaFile();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在读取媒体信息…";

        try
        {
            await Player.LoadAsync(filePath, cancellationToken);
            MediaFileName = Path.GetFileName(filePath);
            StatusMessage = "媒体已就绪";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "导入已取消";
        }
        catch (Exception exception)
        {
            StatusMessage = $"导入失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenMedia() => !IsBusy;

    public void Dispose()
    {
        Player.Dispose();
    }
}
