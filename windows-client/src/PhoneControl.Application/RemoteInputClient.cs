using PhoneControl.Domain;
using PhoneControl.Input;
using PhoneControl.Protocol;

namespace PhoneControl.Application;

public interface IRemoteInputClient
{
    Task SendAsync(MessageEnvelope envelope, CancellationToken cancellationToken);
}

public sealed class RemoteInputClient : IRemoteInputClient
{
    private readonly ConnectionManager _connection;

    public RemoteInputClient(ConnectionManager connection)
    {
        _connection = connection;
    }

    public Task SendAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!_connection.CanSend)
        {
            return Task.CompletedTask;
        }

        return _connection.SendAsync(envelope, cancellationToken);
    }
}

public sealed class PointerInputController
{
    private readonly IRemoteInputClient _client;
    private readonly CoordinateMapper _mapper = new();
    private readonly IClock _clock;

    public PointerInputController(IRemoteInputClient client, IClock clock)
    {
        _client = client;
        _clock = clock;
    }

    private PhonePoint? _down;
    private bool _dragging;
    private long _lastRightTicks;
    private CancellationTokenSource? _rightWait;

    public PhoneScreen Screen { get; set; } = new(1080, 2400, 0);
    public TimeSpan RightClickDelay { get; set; } = TimeSpan.FromMilliseconds(280);

    public Task HandleAsync(
        Win32InputEvent input,
        WindowRect surface,
        double surfaceX,
        double surfaceY,
        CancellationToken cancellationToken)
    {
        var point = _mapper.TryMap(surface, Screen, surfaceX, surfaceY);
        if (point is null &&
            input.Kind is not (Win32InputKind.KeyDown or Win32InputKind.KeyUp or Win32InputKind.TextInput
                or Win32InputKind.MouseRightDown or Win32InputKind.MouseRightUp))
        {
            return Task.CompletedTask;
        }

        switch (input.Kind)
        {
            case Win32InputKind.MouseLeftDown:
                _down = point;
                _dragging = false;
                return Task.CompletedTask;
            case Win32InputKind.MouseMove:
                if (_down is not null && point is not null && Distance(_down.Value, point.Value) > 14)
                {
                    _dragging = true;
                }

                return Task.CompletedTask;
            case Win32InputKind.MouseLeftUp:
                var envelope = EndPointer(point);
                _down = null;
                _dragging = false;
                return envelope is null ? Task.CompletedTask : _client.SendAsync(envelope, cancellationToken);
            case Win32InputKind.MouseRightDown:
                return Task.CompletedTask;
            case Win32InputKind.MouseRightUp:
                return TurnPageAsync(cancellationToken);
            case Win32InputKind.MouseWheel:
                return _client.SendAsync(WheelEnvelope(point, input.Data), cancellationToken);
            case Win32InputKind.TextInput:
            case Win32InputKind.KeyDown:
                if (input.Kind is Win32InputKind.KeyDown &&
                    Win32InputMap.MapKey(input.Data) is "text" &&
                    string.IsNullOrEmpty(input.Text))
                {
                    return Task.CompletedTask;
                }

                return _client.SendAsync(
                    Win32InputMap.ToEnvelope(input, point, _clock.UtcNow.ToUnixTimeMilliseconds()),
                    cancellationToken);
            default:
                return _client.SendAsync(
                    Win32InputMap.ToEnvelope(input, point, _clock.UtcNow.ToUnixTimeMilliseconds()),
                    cancellationToken);
        }
    }

    private MessageEnvelope? EndPointer(PhonePoint? up)
    {
        var start = _down ?? up;
        if (start is null)
        {
            return null;
        }

        var end = up ?? start.Value;
        var ts = _clock.UtcNow.ToUnixTimeMilliseconds();
        if (_dragging || Distance(start.Value, end) > 14)
        {
            return EnvelopeFactory.Create(
                MessageTypes.InputTouch,
                ts,
                new InputTouchPayload
                {
                    Action = "swipe",
                    X = start.Value.X,
                    Y = start.Value.Y,
                    X2 = end.X,
                    Y2 = end.Y,
                    DurationMs = 200
                });
        }

        return EnvelopeFactory.Create(
            MessageTypes.InputTouch,
            ts,
            new InputTouchPayload
            {
                Action = "tap",
                X = start.Value.X,
                Y = start.Value.Y,
                DurationMs = 60
            });
    }

    private MessageEnvelope WheelEnvelope(PhonePoint? point, int wheelDelta)
    {
        var origin = point ?? new PhonePoint(Screen.Width / 2.0, Screen.Height / 2.0);
        var distance = Math.Clamp(Math.Abs(wheelDelta) / 120.0 * Screen.Height * 0.2, 120, Screen.Height * 0.45);
        var sign = Math.Sign(wheelDelta == 0 ? 1 : wheelDelta);
        return EnvelopeFactory.Create(
            MessageTypes.InputTouch,
            _clock.UtcNow.ToUnixTimeMilliseconds(),
            new InputTouchPayload
            {
                Action = "scroll",
                X = origin.X,
                Y = origin.Y,
                X2 = origin.X,
                Y2 = origin.Y - sign * distance,
                DurationMs = 220,
                Delta = wheelDelta
            });
    }

    private async Task TurnPageAsync(CancellationToken cancellationToken)
    {
        var now = Environment.TickCount64;
        var doubled = _lastRightTicks != 0 && now - _lastRightTicks <= 400;
        _lastRightTicks = now;
        if (doubled)
        {
            CancelRightWait();
            await _client.SendAsync(PageSwipeEnvelope(forward: false), cancellationToken).ConfigureAwait(false);
            return;
        }

        CancelRightWait();
        var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _rightWait = wait;
        if (RightClickDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(RightClickDelay, wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (wait.IsCancellationRequested)
        {
            return;
        }

        await _client.SendAsync(PageSwipeEnvelope(forward: true), cancellationToken).ConfigureAwait(false);
    }

    private void CancelRightWait()
    {
        try
        {
            _rightWait?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already cancelled.
        }

        _rightWait?.Dispose();
        _rightWait = null;
    }

    private MessageEnvelope PageSwipeEnvelope(bool forward)
    {
        var y = Screen.Height * 0.5;
        var startX = Screen.Width * (forward ? 0.82 : 0.18);
        var endX = Screen.Width * (forward ? 0.18 : 0.82);
        return EnvelopeFactory.Create(
            MessageTypes.InputTouch,
            _clock.UtcNow.ToUnixTimeMilliseconds(),
            new InputTouchPayload
            {
                Action = "swipe",
                X = startX,
                Y = y,
                X2 = endX,
                Y2 = y,
                DurationMs = 240
            });
    }

    private static double Distance(PhonePoint a, PhonePoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
