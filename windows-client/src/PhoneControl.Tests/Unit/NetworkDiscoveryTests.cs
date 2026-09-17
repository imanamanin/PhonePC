using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;

namespace PhoneControl.Tests.Unit;

public sealed class NetworkDiscoveryTests
{
    [Fact]
    public void DiscoversGateway_OnRndisAdapter()
    {
        var adapters = new[]
        {
            new NetworkAdapterSnapshot(
                "Ethernet 3",
                "Remote NDIS based Internet Sharing Device",
                "Ethernet",
                new[] { "192.168.42.100" },
                new[] { "192.168.42.129" })
        };

        var candidates = TetherEndpointSelector.FromAdapters(adapters);
        candidates.Should().Contain(c => c.Host == "192.168.42.129" && c.Source == "gateway" && !c.IsFallback);
        candidates.Should().OnlyHaveUniqueItems(c => $"{c.Host}:{c.Port}");
        candidates.Should().Contain(c => c.Host == ProtocolPorts.FallbackHost && c.Port == ProtocolPorts.Control);
    }

    [Fact]
    public void AlwaysIncludesFallback_WhenNoAdapters()
    {
        var candidates = TetherEndpointSelector.FromAdapters(Array.Empty<NetworkAdapterSnapshot>());
        candidates.Should().ContainSingle();
        candidates[0].Should().BeEquivalentTo(
            new TetherCandidate(ProtocolPorts.FallbackHost, ProtocolPorts.Control, "fallback", "fallback", true));
    }

    [Fact]
    public async Task Service_UsesProbe()
    {
        var probe = new FakeNetworkInterfaceProbe(new[]
        {
            new NetworkAdapterSnapshot(
                "USB",
                "Android USB NDIS",
                "Ethernet",
                new[] { "192.168.42.20" },
                Array.Empty<string>())
        });
        var service = new NetworkDiscoveryService(probe);
        var found = await service.DiscoverAsync(CancellationToken.None);
        found.Should().Contain(c => c.Host == ProtocolPorts.FallbackHost && c.Source == "subnet-default");
    }

    [Fact]
    public void IgnoresWifiOnDifferentSubnet()
    {
        var adapters = new[]
        {
            new NetworkAdapterSnapshot(
                "Wi-Fi",
                "Intel Wi-Fi",
                "Wireless80211",
                new[] { "192.168.1.20" },
                new[] { "192.168.1.1" })
        };
        var candidates = TetherEndpointSelector.FromAdapters(adapters);
        candidates.Should().ContainSingle(c => c.IsFallback);
    }

    [Fact]
    public void DiscoversRndisGateway_OnModernTenDotSubnet()
    {
        var adapters = new[]
        {
            new NetworkAdapterSnapshot(
                "Ethernet 5",
                "Remote NDIS Compatible Device #2",
                "Ethernet",
                new[] { "10.242.187.143" },
                new[] { "10.242.187.225" },
                new[] { "10.242.187.225" })
        };

        var candidates = TetherEndpointSelector.FromAdapters(adapters);
        candidates.Should().Contain(c => c.Host == "10.242.187.225" && !c.IsFallback);
        candidates.First(c => !c.IsFallback).Host.Should().Be("10.242.187.225");
        candidates.Should().Contain(c => c.IsFallback);
    }

    [Fact]
    public void InfersSlash24Gateway_WhenRndisHasNoGateway()
    {
        var adapters = new[]
        {
            new NetworkAdapterSnapshot(
                "Ethernet 5",
                "Remote NDIS Compatible Device #2",
                "Ethernet",
                new[] { "10.242.187.143" },
                Array.Empty<string>())
        };

        var candidates = TetherEndpointSelector.FromAdapters(adapters);
        candidates.Should().Contain(c => c.Host == "10.242.187.1" && c.Source == "inferred-gateway" && !c.IsFallback);
        candidates.First(c => !c.IsFallback).Host.Should().Be("10.242.187.1");
    }

    [Fact]
    public void IgnoresUsbWifiAdapter()
    {
        var adapters = new[]
        {
            new NetworkAdapterSnapshot(
                "Wi-Fi 2",
                "TP-Link Wireless USB Adapter",
                "Wireless80211",
                new[] { "192.168.100.102" },
                new[] { "192.168.100.1" })
        };
        var candidates = TetherEndpointSelector.FromAdapters(adapters);
        candidates.Should().ContainSingle(c => c.IsFallback);
    }
}
