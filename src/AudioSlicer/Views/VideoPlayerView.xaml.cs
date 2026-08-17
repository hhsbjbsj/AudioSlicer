using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AudioSlicer.ViewModels;

namespace AudioSlicer.Views;

public partial class VideoPlayerView : UserControl
{
    public VideoPlayerView()
    {
        InitializeComponent();
    }

    private void OnSeekDragStarted(object sender, DragStartedEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.BeginSeeking();
        }
    }

    private void OnSeekDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.EndSeeking();
        }
    }
}

