using FluentAssertions;
using PhoneControl.Adb;
using PhoneControl.Application;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;

namespace PhoneControl.Tests.Unit;

public sealed class CompositeDeviceDiscoveryTests
{
    [Fact]
    public async Task AdbFailure_DoesNotThrow_AndFallsBackToTetherCandidate()
    {
        var discovery = new CompositeDeviceDiscovery(new ThrowingAdbClient(), new FakeNetworkDiscovery());
        var devices = await discovery.DiscoverAsync(CancellationToken.None);
        devices.Should().NotBeEmpty();
        devices[0].AdbAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task FakeAdb_IsListed()
    {
        var adb = new FakeAdbClient
        {
            Devices = new[] { new AdbDevice("SERIAL", "device", "Pixel") }
        };
        var network = new FakeNetworkDiscovery(new[]
        {
            new TetherCandidate("192.168.42.129", 17890, "RNDIS", "gateway", false)
        });
        var discovery = new CompositeDeviceDiscovery(adb, network);
        var devices = await discovery.DiscoverAsync(CancellationToken.None);
        devices[0].AdbAvailable.Should().BeTrue();
        devices[0].DisplayName.Should().Be("Pixel");
        devices[0].TetherEndpoint.Should().Be("192.168.42.129:17890");
        devices[0].TetheringLikely.Should().BeTrue();
    }

    private sealed class ThrowingAdbClient : IAdbClient
    {
        public Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("adb missing");
        }
    }
}
