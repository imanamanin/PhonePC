using System.Windows;
using PhoneControl.Application;
using PhoneControl.Desktop.ViewModels;
using PhoneControl.Screen;

namespace PhoneControl.Desktop;

public partial class MainWindow : Window
{
    private readonly VideoSession _video;

    public MainWindow(MainViewModel viewModel, VideoSession video, PointerInputController input)
    {
        InitializeComponent();
        DataContext = viewModel;
        _video = video;
        PhoneView.Attach(input);
        _video.FrameArrived += OnFrame;
        Closed += (_, _) => _video.FrameArrived -= OnFrame;
    }

    private void OnFrame(object? sender, DecodedFrame frame)
    {
        Dispatcher.Invoke(() => PhoneView.Present(frame));
    }
}
