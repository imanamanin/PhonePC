using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

/// <summary>
/// Combines USB/tether hints with optional ADB. ADB failure must not fail discovery
/// and must never change USB functions.
/// </summary>
public sealed class CompositeDeviceDiscovery : IDeviceDiscovery
{
    private readonly IAdbClient _adb;
    private readonly INetworkDiscoveryService _network;

    public CompositeDeviceDiscovery(IAdbClient adb, INetworkDiscoveryService network)
    {
        _adb = adb;
        _network = network;
    }

    public async Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var network = await _network.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var primary = network.FirstOrDefault(c => !c.IsFallback) ?? network.FirstOrDefault()
            ?? new TetherCandidate(ProtocolPorts.FallbackHost, ProtocolPorts.Control, "fallback", "fallback", true);
        var endpoint = $"{primary.Host}:{primary.Port}";

        IReadOnlyList<AdbDevice> adbDevices;
        try
        {
            adbDevices = await _adb.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            adbDevices = Array.Empty<AdbDevice>();
        }

        var results = new List<DiscoveredDevice>();
        foreach (var device in adbDevices)
        {
            var serial = device.Serial;
            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(serial)))[..12];
            results.Add(new DiscoveredDevice(
                device.Model ?? device.Serial,
                hash,
                endpoint,
                AdbAvailable: true,
                UsbPresent: true,
                TetheringLikely: !primary.IsFallback));
        }

        if (results.Count == 0)
        {
            results.Add(new DiscoveredDevice(
                primary.IsFallback ? "USB tether candidate" : primary.AdapterName,
                null,
                endpoint,
                AdbAvailable: false,
                UsbPresent: !primary.IsFallback,
                TetheringLikely: !primary.IsFallback));
        }

        return results;
    }
}
