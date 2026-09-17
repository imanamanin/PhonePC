using PhoneControl.Domain;

namespace PhoneControl.Application;

public sealed class ConnectionOptions
{
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromMilliseconds(ProtocolPorts.DefaultTimeoutMs);
    public TimeSpan TcpConnectTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromMilliseconds(ProtocolPorts.HeartbeatIntervalMs);
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromMilliseconds(ProtocolPorts.DefaultTimeoutMs);
    public int MaxReconnectAttempts { get; init; } = 8;
    public int ReconnectInitialDelayMs { get; init; } = 500;
    public int ReconnectMaxDelayMs { get; init; } = 8000;
    public string ClientName { get; init; } = "PhoneControl.Desktop";
    public string ClientVersion { get; init; } = "0.1.0";
}

public sealed class ConnectionDependencies
{
    public required IDeviceDiscovery Discovery { get; init; }
    public required INetworkDiscoveryService Network { get; init; }
    public required IControlTransportFactory TransportFactory { get; init; }
    public required IClock Clock { get; init; }
    public required IDelay Delay { get; init; }
    public required IAppLogger Logger { get; init; }
    public required ISecureStore SecureStore { get; init; }
    public ConnectionOptions Options { get; init; } = new();
}

public sealed class NetworkDiscoveryService : INetworkDiscoveryService
{
    private readonly INetworkInterfaceProbe _probe;

    public NetworkDiscoveryService(INetworkInterfaceProbe probe)
    {
        _probe = probe;
    }

    public Task<IReadOnlyList<TetherCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<TetherCandidate> candidates = TetherEndpointSelector.FromAdapters(_probe.ListUpAdapters());
        return Task.FromResult(candidates);
    }
}

public sealed class ImmediateDelay : IDelay
{
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class TaskDelay : IDelay
{
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
