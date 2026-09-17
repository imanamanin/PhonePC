using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

public static class LengthPrefixedFrame
{
    public static async Task WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => await WriteAsync(stream, payload, ProtocolPorts.MaxJsonFrameBytes, cancellationToken).ConfigureAwait(false);

    public static async Task WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (payload.Length <= 0 || payload.Length > maxBytes)
        {
            throw new InvalidOperationException("Invalid frame length.");
        }

        var header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => await ReadAsync(stream, ProtocolPorts.MaxJsonFrameBytes, cancellationToken).ConfigureAwait(false);

    public static async Task<byte[]> ReadAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
        if (size <= 0 || size > maxBytes)
        {
            throw new InvalidOperationException("Invalid frame length.");
        }

        var body = new byte[size];
        await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
        return body;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}

public sealed class TcpControlTransportFactory : IControlTransportFactory
{
    public IControlTransport Create() => new TcpControlTransport();
}

public sealed class WindowsNetworkInterfaceProbe : INetworkInterfaceProbe
{
    public IReadOnlyList<NetworkAdapterSnapshot> ListUpAdapters()
    {
        var list = new List<NetworkAdapterSnapshot>();
        foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
            {
                continue;
            }

            try
            {
                var props = nic.GetIPProperties();
                var unicast = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .ToArray();
                var gateways = props.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(g => g.Address.ToString())
                    .ToArray();
                var dhcp = OperatingSystem.IsWindows()
                    ? props.DhcpServerAddresses
                        .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.ToString())
                        .ToArray()
                    : Array.Empty<string>();
                list.Add(new NetworkAdapterSnapshot(
                    nic.Name,
                    nic.Description,
                    nic.NetworkInterfaceType.ToString(),
                    unicast,
                    gateways,
                    dhcp));
            }
            catch (NetworkInformationException)
            {
                // Some virtual adapters throw; skip them.
            }
        }

        return list;
    }
}
