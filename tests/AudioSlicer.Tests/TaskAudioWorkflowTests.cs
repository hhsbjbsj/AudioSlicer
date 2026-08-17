using System.IO;
using System.Threading;
using System.Windows.Threading;
using AudioSlicer.Export;
using AudioSlicer.Media;
using AudioSlicer.Project;
using AudioSlicer.Services;
using AudioSlicer.ViewModels;
using AudioSlicer.Waveform;
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
                    await viewModel.LoadMediaAsync(source, CancellationToken.None);
                    viewModel.Waveform.SetSelection(0.25, 0.80);
                    viewModel.AddSegmentCommand.Execute(null);
                    if (viewModel.AddSegmentCommand.ExecutionTask is { } task) await task;

                    var item = Assert.Single(viewModel.Segments.Segments);
                    Assert.True(item.HasAudioFile);
                    generatedAudio = item.AudioFilePath;
                    var info = await new FFprobeService(locator).ProbeAsync(item.AudioFilePath, CancellationToken.None);
                    Assert.InRange(info.Duration.TotalSeconds, 0.54, 0.56);
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
