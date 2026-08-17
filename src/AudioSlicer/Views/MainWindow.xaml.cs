using System.Windows;
using AudioSlicer.ViewModels;

namespace AudioSlicer.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

