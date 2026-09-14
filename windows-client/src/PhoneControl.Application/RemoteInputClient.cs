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

    public PhoneScreen Screen { get; set; } = new(1080, 2400, 0);

    public Task HandleAsync(
        Win32InputEvent input,
        WindowRect surface,
        double surfaceX,
        double surfaceY,
        CancellationToken cancellationToken)
    {
        var point = _mapper.TryMap(surface, Screen, surfaceX, surfaceY);
        if (point is null && input.Kind is not (Win32InputKind.KeyDown or Win32InputKind.KeyUp))
        {
            return Task.CompletedTask;
        }

        var envelope = Win32InputMap.ToEnvelope(
            input,
            point,
            _clock.UtcNow.ToUnixTimeMilliseconds());
        return _client.SendAsync(envelope, cancellationToken);
    }
}
