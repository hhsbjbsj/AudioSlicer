using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using AudioSlicer.ViewModels;

namespace AudioSlicer.Views;

public partial class SegmentListView : UserControl
{
    public SegmentListView()
    {
        InitializeComponent();
    }

    private MainViewModel? MainViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MainViewModel?.SetSelectedSegments(SegmentList.SelectedItems.Cast<SegmentItemViewModel>().ToArray());
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SegmentList.SelectedItem is SegmentItemViewModel item) MainViewModel?.ActivateSegmentCommand.Execute(item);
    }

    private void OnNameGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox) textBox.Tag = textBox.Text;
    }

    private void OnNameLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: SegmentItemViewModel item } textBox && textBox.Tag is string oldName)
        {
            MainViewModel?.CommitRename(item, oldName);
        }
    }
}
