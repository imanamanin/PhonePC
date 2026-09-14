using PhoneControl.Domain;

namespace PhoneControl.Application;

public sealed class FakeNetworkDiscovery : INetworkDiscoveryService
{
    public FakeNetworkDiscovery(IReadOnlyList<TetherCandidate>? candidates = null)
    {
        Candidates = candidates ?? new[]
        {
            new TetherCandidate(ProtocolPorts.FallbackHost, ProtocolPorts.Control, "fallback", "fallback", true)
        };
    }

    public IReadOnlyList<TetherCandidate> Candidates { get; }

    public Task<IReadOnlyList<TetherCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Candidates);
    }
}

public sealed class FakeNetworkInterfaceProbe : INetworkInterfaceProbe
{
    public FakeNetworkInterfaceProbe(IReadOnlyList<NetworkAdapterSnapshot>? adapters = null)
    {
        Adapters = adapters ?? Array.Empty<NetworkAdapterSnapshot>();
    }

    public IReadOnlyList<NetworkAdapterSnapshot> Adapters { get; }

    public IReadOnlyList<NetworkAdapterSnapshot> ListUpAdapters() => Adapters;
}
