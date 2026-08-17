using System.IO;
using System.Windows;
using System.Windows.Threading;
using AudioSlicer.Media;
using AudioSlicer.Services;
using AudioSlicer.ViewModels;
using AudioSlicer.Views;

namespace AudioSlicer;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var playerService = new LibVlcVideoPlayerService();
            var fileDialogService = new FileDialogService();
            var ffmpegLocator = new FFmpegLocator();
            var ffprobeService = new FFprobeService(ffmpegLocator);
            var waveformCache = new Waveform.WaveformCache();
            var waveformGenerator = new Waveform.WaveformGenerator(ffmpegLocator, waveformCache);
            var projectSerializer = new Project.ProjectSerializer();
            var audioExporter = new Export.AudioExporter(ffmpegLocator);
            var audioPreviewService = new AudioPreviewService();

            _mainViewModel = new MainViewModel(playerService, fileDialogService, ffprobeService, waveformGenerator, projectSerializer, audioExporter, audioPreviewService);
            MainWindow = new MainWindow(_mainViewModel);
            MainWindow.Show();
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                _ = _mainViewModel.LoadMediaAsync(e.Args[0], CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Services.AppLogger.Error("应用启动失败", exception);
            MessageBox.Show(
                $"Audio Slicer 无法启动。\n\n{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"操作未能完成。\n\n{e.Exception.Message}",
            "Audio Slicer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Services.AppLogger.Error("未处理的界面异常", e.Exception);
    }
}
