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

            foreach (var gateway in adapter.GatewayIpv4)
            {
                if (IsTetherIpv4(gateway))
                {
                    Add(results, seen, gateway, port, adapter.Name, "gateway", isFallback: false);
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
        if (ContainsAny(blob, "RNDIS", "NDIS", "Remote NDIS", "USB", "Android", "tether", "Tethering"))
        {
            return true;
        }

        if (!IsEthernetFamily(adapter.Type))
        {
            return adapter.UnicastIpv4.Any(IsTetherIpv4) || adapter.GatewayIpv4.Any(IsTetherIpv4);
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
