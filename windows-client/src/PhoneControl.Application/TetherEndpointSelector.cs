using System.Net;
using PhoneControl.Domain;

namespace PhoneControl.Application;

public static class TetherEndpointSelector
{
    private static readonly string[] SubnetPrefixes = { "192.168.42.", "192.168.137." };

    public static IReadOnlyList<TetherCandidate> FromAdapters(
        IReadOnlyList<NetworkAdapterSnapshot> adapters,
        int port = ProtocolPorts.Control)
    {
        var results = new List<TetherCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var adapter in adapters)
        {
            if (!IsTetherLike(adapter))
            {
                continue;
            }

            // Modern Android USB tethering often uses 10.x (not only 192.168.42.129).
            foreach (var gateway in adapter.GatewayIpv4)
            {
                if (IsUsableHost(gateway) && !IsOwnAddress(adapter, gateway))
                {
                    Add(results, seen, gateway, port, adapter.Name, "gateway", isFallback: false);
                }
            }

            foreach (var dhcp in adapter.DhcpServerIpv4 ?? Array.Empty<string>())
            {
                if (IsUsableHost(dhcp) && !IsOwnAddress(adapter, dhcp))
                {
                    Add(results, seen, dhcp, port, adapter.Name, "dhcp", isFallback: false);
                }
            }

            if (!results.Any(c => !c.IsFallback && c.AdapterName == adapter.Name))
            {
                foreach (var ip in adapter.UnicastIpv4)
                {
                    if (TryInferSlash24Gateway(ip, out var inferred) && !IsOwnAddress(adapter, inferred))
                    {
                        Add(results, seen, inferred, port, adapter.Name, "inferred-gateway", isFallback: false);
                    }
                }
            }

            if (adapter.UnicastIpv4.Any(ip => ip.StartsWith("192.168.42.", StringComparison.Ordinal)))
            {
                Add(results, seen, ProtocolPorts.FallbackHost, port, adapter.Name, "subnet-default", isFallback: false);
            }

            if (adapter.UnicastIpv4.Any(ip => ip.StartsWith("192.168.137.", StringComparison.Ordinal)))
            {
                Add(results, seen, "192.168.137.1", port, adapter.Name, "subnet-default", isFallback: false);
            }
        }

        Add(results, seen, ProtocolPorts.FallbackHost, port, "fallback", "fallback", isFallback: true);
        return results;
    }

    public static bool IsTetherLike(NetworkAdapterSnapshot adapter)
    {
        var blob = $"{adapter.Name} {adapter.Description} {adapter.Type}";
        if (ContainsAny(blob, "Wireless", "Wi-Fi", "WiFi", "802.11"))
        {
            return adapter.UnicastIpv4.Any(IsTetherIpv4) || adapter.GatewayIpv4.Any(IsTetherIpv4);
        }

        if (ContainsAny(blob, "RNDIS", "Remote NDIS", "Android", "tether", "Tethering"))
        {
            return true;
        }

        if (ContainsAny(blob, "USB", "NDIS") && IsEthernetFamily(adapter.Type))
        {
            return true;
        }

        return adapter.UnicastIpv4.Any(IsTetherIpv4) || adapter.GatewayIpv4.Any(IsTetherIpv4);
    }

    public static bool IsTetherIpv4(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        return SubnetPrefixes.Any(prefix => address.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static bool IsUsableHost(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(parsed))
        {
            return false;
        }

        var bytes = parsed.GetAddressBytes();
        return bytes[0] is not 0 and < 224;
    }

    private static bool IsOwnAddress(NetworkAdapterSnapshot adapter, string host)
    {
        return adapter.UnicastIpv4.Contains(host, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryInferSlash24Gateway(string unicast, out string inferred)
    {
        inferred = string.Empty;
        if (!IPAddress.TryParse(unicast, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = parsed.GetAddressBytes();
        if (bytes[3] == 1)
        {
            return false;
        }

        inferred = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.1";
        return IsUsableHost(inferred);
    }

    private static bool IsEthernetFamily(string type)
    {
        return type.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        return needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static void Add(
        List<TetherCandidate> results,
        HashSet<string> seen,
        string host,
        int port,
        string adapterName,
        string source,
        bool isFallback)
    {
        var key = $"{host}:{port}";
        if (!seen.Add(key))
        {
            return;
        }

        results.Add(new TetherCandidate(host, port, adapterName, source, isFallback));
    }
}
