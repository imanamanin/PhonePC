namespace PhoneControl.Domain;

public sealed record NetworkAdapterSnapshot(
    string Name,
    string Description,
    string Type,
    IReadOnlyList<string> UnicastIpv4,
    IReadOnlyList<string> GatewayIpv4,
    IReadOnlyList<string>? DhcpServerIpv4 = null);

public sealed record TetherCandidate(
    string Host,
    int Port,
    string AdapterName,
    string Source,
    bool IsFallback);

public interface INetworkInterfaceProbe
{
    IReadOnlyList<NetworkAdapterSnapshot> ListUpAdapters();
}

public interface INetworkDiscoveryService
{
    Task<IReadOnlyList<TetherCandidate>> DiscoverAsync(CancellationToken cancellationToken);
}

public interface IControlTransportFactory
{
    IControlTransport Create();
}

public interface IVideoTransport : IAsyncDisposable
{
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPacketsAsync(CancellationToken cancellationToken);
    bool IsConnected { get; }
}

public interface IVideoTransportFactory
{
    IVideoTransport Create();
}

public interface IDelay
{
    Task Delay(TimeSpan duration, CancellationToken cancellationToken);
}
