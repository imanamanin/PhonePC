using PhoneControl.Domain;

namespace PhoneControl.Application;

public sealed class MockDeviceDiscovery : IDeviceDiscovery
{
    private readonly IReadOnlyList<DiscoveredDevice> _devices;

    public MockDeviceDiscovery(IReadOnlyList<DiscoveredDevice>? devices = null)
    {
        _devices = devices ?? new[]
        {
            new DiscoveredDevice(
                "Mock Pixel",
                "abc123",
                "192.168.42.129",
                AdbAvailable: false,
                UsbPresent: true,
                TetheringLikely: true)
        };
    }

    public Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_devices);
    }
}
