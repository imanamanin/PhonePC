using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Tests.Unit;

namespace PhoneControl.Tests.Integration;

public sealed class HandshakeHeartbeatTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoopbackTcp_HandshakeAndHeartbeat()
    {
        await using var server = await LoopbackAgentServer.StartAsync(pairingRequired: false);
        var network = new FakeNetworkDiscovery(new[]
        {
            new TetherCandidate("127.0.0.1", server.Port, "loopback", "loopback", false)
        });
        await using var manager = ConnectionHarness.Create(
            transport: new TcpControlTransportFactory(),
            network: network,
            discovery: new MockDeviceDiscovery(new[]
            {
                new DiscoveredDevice("Loopback", "hash", $"127.0.0.1:{server.Port}", false, true, true)
            }),
            delay: new TaskDelay(),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                HeartbeatInterval = TimeSpan.FromMilliseconds(40),
                HeartbeatTimeout = TimeSpan.FromSeconds(2),
                MaxReconnectAttempts = 1,
                ReconnectInitialDelayMs = 10,
                ReconnectMaxDelayMs = 10
            });

        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        manager.Snapshot.Device!.Model.Should().Be("Loopback Agent");

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (server.PongCount < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(30);
        }

        server.HelloCount.Should().BeGreaterThan(0);
        server.PongCount.Should().BeGreaterThan(0);
        await manager.DisconnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoopbackTcp_PairingRequired_KeepsSession()
    {
        await using var server = await LoopbackAgentServer.StartAsync(pairingRequired: true);
        var network = new FakeNetworkDiscovery(new[]
        {
            new TetherCandidate("127.0.0.1", server.Port, "loopback", "loopback", false)
        });
        await using var manager = ConnectionHarness.Create(
            transport: new TcpControlTransportFactory(),
            network: network,
            delay: new TaskDelay(),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                HeartbeatInterval = TimeSpan.FromMilliseconds(40),
                HeartbeatTimeout = TimeSpan.FromSeconds(2),
                MaxReconnectAttempts = 1,
                ReconnectInitialDelayMs = 10,
                ReconnectMaxDelayMs = 10
            });

        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        await Task.Delay(120);
        server.PongCount.Should().BeGreaterThan(0);
        await manager.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoopbackTcp_SubmitPin_BecomesTrusted()
    {
        await using var server = await LoopbackAgentServer.StartAsync(pairingRequired: true);
        var network = new FakeNetworkDiscovery(new[]
        {
            new TetherCandidate("127.0.0.1", server.Port, "loopback", "loopback", false)
        });
        var store = new InMemorySecureStore();
        await using var manager = ConnectionHarness.Create(
            transport: new TcpControlTransportFactory(),
            network: network,
            delay: new TaskDelay(),
            secureStore: store,
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                HeartbeatInterval = TimeSpan.FromMilliseconds(40),
                HeartbeatTimeout = TimeSpan.FromSeconds(2),
                MaxReconnectAttempts = 1,
                ReconnectInitialDelayMs = 10,
                ReconnectMaxDelayMs = 10
            });

        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        await manager.SubmitPinAsync(server.ExpectedPin, CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        manager.Snapshot.TrustedDevice.Should().BeTrue();
        manager.Snapshot.Status!.BatteryPercent.Should().Be(64);
        server.PairingAcceptedCount.Should().Be(1);
        await manager.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoopbackTcp_SubmitPin_SurvivesSlowPhoneReply()
    {
        await using var server = await LoopbackAgentServer.StartAsync(
            pairingRequired: true,
            pairingReplyDelay: TimeSpan.FromMilliseconds(180));
        var network = new FakeNetworkDiscovery(new[]
        {
            new TetherCandidate("127.0.0.1", server.Port, "loopback", "loopback", false)
        });
        await using var manager = ConnectionHarness.Create(
            transport: new TcpControlTransportFactory(),
            network: network,
            delay: new TaskDelay(),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                HeartbeatInterval = TimeSpan.FromMilliseconds(20),
                HeartbeatTimeout = TimeSpan.FromMilliseconds(80),
                MaxReconnectAttempts = 1,
                ReconnectInitialDelayMs = 10,
                ReconnectMaxDelayMs = 10
            });

        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        await manager.SubmitPinAsync(server.ExpectedPin, CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        await manager.DisconnectAsync(CancellationToken.None);
    }
}
