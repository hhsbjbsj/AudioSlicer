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

            _mainViewModel = new MainViewModel(playerService, fileDialogService);
            MainWindow = new MainWindow(_mainViewModel);
            MainWindow.Show();
        }
        catch (Exception exception)
        {
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
    }
}

