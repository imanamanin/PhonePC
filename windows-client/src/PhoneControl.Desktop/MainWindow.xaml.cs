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
        _video.HudChanged += OnHud;
        Closed += (_, _) =>
        {
            _video.FrameArrived -= OnFrame;
            _video.HudChanged -= OnHud;
        };
    }

    private void OnHud(object? sender, StreamHud hud)
    {
        Dispatcher.Invoke(() => PhoneView.SetStatus(StatusFor(hud)));
    }

    private static string StatusFor(StreamHud hud) => hud.Codec switch
    {
        "need-capture" => "On the phone tap Start screen capture and accept the system dialog.",
        "offline" => "Video TCP 17891 is not open yet. Keep the capture notification on the phone.",
        "none" => "Channel C waiting on TCP 17891",
        "H264" => "Receiving H.264 — waiting for the first decoded frame",
        "Jpeg" => "Receiving JPEG — waiting for the first decoded frame",
        _ => string.IsNullOrEmpty(hud.Codec) ? "Channel C waiting on TCP 17891" : hud.Codec
    };

    private void OnFrame(object? sender, DecodedFrame frame)
    {
        try
        {
            Dispatcher.Invoke(() => PhoneView.Present(frame));
        }
        catch (Exception)
        {
            // A bad frame must not close the window.
        }
    }
}
