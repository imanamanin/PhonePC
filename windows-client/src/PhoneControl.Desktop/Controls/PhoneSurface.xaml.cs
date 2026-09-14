using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhoneControl.Application;
using PhoneControl.Input;
using PhoneControl.Screen;

namespace PhoneControl.Desktop.Controls;

public partial class PhoneSurface : UserControl
{
    private PointerInputController? _input;
    private WriteableBitmap? _bitmap;
    private long _lastMoveTicks;

    public PhoneSurface()
    {
        InitializeComponent();
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
        ManipulationDelta += OnPinch;
        Loaded += (_, _) => Focus();
    }

    public void Attach(PointerInputController input) => _input = input;

    public void Present(DecodedFrame frame)
    {
        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            FrameImage.Source = _bitmap;
        }

        _bitmap.Lock();
        try
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Bgra, frame.Width * 4, 0);
        }
        finally
        {
            _bitmap.Unlock();
        }

        Placeholder.Visibility = Visibility.Collapsed;
        if (_input is not null)
        {
            _input.Screen = new PhoneScreen(frame.Width, frame.Height, 0);
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        Dispatch(NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventLeftDown), e.GetPosition(this));
        Focus();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        Dispatch(NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventLeftUp), e.GetPosition(this));
        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var now = Environment.TickCount64;
        if (now - _lastMoveTicks < 20)
        {
            return;
        }

        _lastMoveTicks = now;
        Dispatch(NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventMove), e.GetPosition(this));
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Dispatch(NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventWheel, e.Delta), e.GetPosition(this));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_input is null)
        {
            return;
        }

        var vk = KeyInterop.VirtualKeyFromKey(e.Key);
        var ev = new Win32InputEvent(Win32InputKind.KeyDown, 0, 0, vk, 1);
        _ = _input.HandleAsync(ev, SurfaceRect(), 0, 0, CancellationToken.None);
        e.Handled = true;
    }

    private void OnPinch(object? sender, ManipulationDeltaEventArgs e)
    {
        if (Math.Abs(e.DeltaManipulation.Scale.X - 1) < 0.02)
        {
            return;
        }

        var ev = new Win32InputEvent(
            Win32InputKind.Pinch,
            0,
            0,
            (int)(e.DeltaManipulation.Scale.X * 100),
            e.DeltaManipulation.Scale.X);
        Dispatch(ev, e.ManipulationOrigin);
    }

    private void Dispatch(Win32InputEvent input, Point pos)
    {
        if (_input is null)
        {
            return;
        }

        _ = _input.HandleAsync(input, SurfaceRect(), pos.X, pos.Y, CancellationToken.None);
    }

    private WindowRect SurfaceRect() => new(ActualWidth, ActualHeight);
}
