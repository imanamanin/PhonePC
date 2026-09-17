using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Tests.Fakes;

namespace PhoneControl.Tests.Unit;

public sealed class ConnectionManagerTests
{
    [Fact]
    public async Task Connect_WithoutEndpoint_GoesToError()
    {
        var discovery = new MockDeviceDiscovery(new[]
        {
            new DiscoveredDevice("Empty", null, null, false, false, false)
        });
        await using var manager = ConnectionHarness.Create(
            network: new FakeNetworkDiscovery(Array.Empty<TetherCandidate>()),
            discovery: discovery);
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Error);
        manager.Snapshot.ErrorCode.Should().Be("NO_ENDPOINT");
    }

    [Fact]
    public async Task Connect_WithHandshake_RequiresPairing()
    {
        await using var manager = ConnectionHarness.Create();
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.PairingRequired);
        manager.Snapshot.Device!.Model.Should().Be("Loopback Phone");
        manager.Snapshot.TetheringLikely.Should().BeTrue();
        await manager.DisconnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task Connect_WhenAlreadyPaired_GoesToConnected()
    {
        await using var manager = ConnectionHarness.Create(
            transport: new AutoReplyingTransportFactory(() => new AutoReplyingTransport(pairingRequired: false)));
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);
        await manager.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandshakeTimeout_ReconnectsThenErrors()
    {
        var states = new List<ConnectionState>();
        await using var manager = ConnectionHarness.Create(
            transport: new AutoReplyingTransportFactory(() => new AutoReplyingTransport(replyToHello: false, autoPong: false)),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromMilliseconds(40),
                HeartbeatInterval = TimeSpan.FromMilliseconds(40),
                HeartbeatTimeout = TimeSpan.FromMilliseconds(40),
                MaxReconnectAttempts = 2,
                ReconnectInitialDelayMs = 1,
                ReconnectMaxDelayMs = 1
            });
        manager.StateChanged += (_, snap) => states.Add(snap.State);
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Error);
        manager.Snapshot.ErrorCode.Should().Be("PHONE_UNREACHABLE");
        states.Should().Contain(ConnectionState.Connecting);
        states.Should().Contain(ConnectionState.Reconnecting);
        states.Should().Contain(ConnectionState.Error);
    }

    [Fact]
    public async Task HeartbeatTimeout_MovesToReconnecting()
    {
        var states = new List<ConnectionState>();
        await using var manager = ConnectionHarness.Create(
            transport: new AutoReplyingTransportFactory(() => new AutoReplyingTransport(pairingRequired: false, autoPong: false)),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromMilliseconds(200),
                HeartbeatInterval = TimeSpan.FromMilliseconds(1),
                HeartbeatTimeout = TimeSpan.FromMilliseconds(40),
                MaxReconnectAttempts = 8,
                ReconnectInitialDelayMs = 1,
                ReconnectMaxDelayMs = 1
            });
        manager.StateChanged += (_, snap) => states.Add(snap.State);
        await manager.ConnectAsync(CancellationToken.None);
        manager.Snapshot.State.Should().Be(ConnectionState.Connected);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!states.Contains(ConnectionState.Reconnecting) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        states.Should().Contain(ConnectionState.Connected);
        states.Should().Contain(ConnectionState.Reconnecting);
        await manager.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UserCancel_DuringConnect_ReturnsToDisconnected()
    {
        await using var manager = ConnectionHarness.Create(
            transport: new AutoReplyingTransportFactory(() => new HangingTransport()),
            options: new ConnectionOptions
            {
                HandshakeTimeout = TimeSpan.FromSeconds(5),
                MaxReconnectAttempts = 1,
                ReconnectInitialDelayMs = 1,
                ReconnectMaxDelayMs = 1
            });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await manager.ConnectAsync(cts.Token);
        manager.Snapshot.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public void ReconnectPolicy_GrowsThenCaps()
    {
        var policy = new ReconnectPolicy(500, 2000);
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(500));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(1000));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(2000));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(2000));
        policy.Reset();
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(500));
    }
}
