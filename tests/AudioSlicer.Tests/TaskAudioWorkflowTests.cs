using System.IO;
using System.Threading;
using System.Windows.Threading;
using AudioSlicer.Export;
using AudioSlicer.Media;
using AudioSlicer.Models;
using AudioSlicer.Project;
using AudioSlicer.Services;
using AudioSlicer.ViewModels;
using AudioSlicer.Waveform;
using AudioSlicer.Views;
using Xunit;

namespace AudioSlicer.Tests;

public sealed class TaskAudioWorkflowTests
{
    [Fact]
    public async Task SelectedRange_CreatesIndependentAudioFileInTaskList()
    {
        var locator = new FFmpegLocator();
        var directory = Path.Combine(Path.GetTempPath(), $"AudioSlicerWorkflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.mp4");
        await ProcessRunner.RunForTextAsync(
            locator.FFmpegPath,
            ["-y", "-f", "lavfi", "-i", "color=c=black:s=320x180:d=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:v", "libx264", "-c:a", "aac", "-shortest", source],
            CancellationToken.None);

        string? generatedAudio = null;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                MainViewModel? viewModel = null;
                try
                {
                    if (System.Windows.Application.Current is null)
                    {
                        var application = new App();
                        application.InitializeComponent();
                    }
                    var player = new LibVlcVideoPlayerService();
                    var cache = new WaveformCache();
                    var exporter = new AudioExporter(locator);
                    viewModel = new MainViewModel(
                        player,
                        new NullFileDialogService(),
                        new FFprobeService(locator),
                        new WaveformGenerator(locator, cache),
                        new ProjectSerializer(),
                        exporter,
                        new AudioPreviewService());
                    var window = new MainWindow(viewModel);
                    var segmentPanel = Assert.IsType<SegmentListView>(window.FindName("SegmentPanel"));
                    Assert.Same(viewModel, segmentPanel.DataContext);
                    await viewModel.LoadMediaAsync(source, CancellationToken.None);
                    viewModel.Waveform.SetSelection(0.25, 0.80);
                    viewModel.AddSegmentCommand.Execute(null);
                    if (viewModel.AddSegmentCommand.ExecutionTask is { } task) await task;

                    var item = Assert.Single(viewModel.Segments.Segments);
                    Assert.True(item.HasAudioFile);
                    generatedAudio = item.AudioFilePath;
                    var info = await new FFprobeService(locator).ProbeAsync(item.AudioFilePath, CancellationToken.None);
                    Assert.InRange(info.Duration.TotalSeconds, 0.54, 0.56);

                    var secondItem = viewModel.Segments.Add(new Segment
                    {
                        Name = "保留片段",
                        StartTime = TimeSpan.FromSeconds(1.0),
                        EndTime = TimeSpan.FromSeconds(1.5),
                    });
                    item.IsExportSelected = true;
                    secondItem.IsExportSelected = false;
                    viewModel.DeleteCheckedSegmentsCommand.Execute(null);
                    Assert.Same(secondItem, Assert.Single(viewModel.Segments.Segments));
                    Assert.Contains("已删除 1 个勾选音频", viewModel.StatusMessage);

                    viewModel.DeleteSegmentCommand.Execute(secondItem);
                    Assert.Empty(viewModel.Segments.Segments);
                    Assert.Contains("已删除：保留片段", viewModel.StatusMessage);
                    window.Close();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
                finally
                {
                    viewModel?.Dispose();
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
            thread.Join(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(generatedAudio) && File.Exists(generatedAudio)) File.Delete(generatedAudio);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class NullFileDialogService : IFileDialogService
    {
        public string? SelectMediaFile() => null;
        public string? SelectProjectToOpen() => null;
        public string? SelectProjectToSave(string suggestedName) => null;
        public string? SelectExportDirectory() => null;
        public string? SelectZipPath(string suggestedName) => null;
    }
}
