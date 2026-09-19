using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

internal static class Program
{
    private static int Main()
    {
        try
        {
            ReadIgnoredRequest();
            var ips = new List<string>();
            var ifaces = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                var name = Escape(string.IsNullOrEmpty(nic.Name) ? nic.Description : nic.Name);
                var gateway = "";
                foreach (var gw in nic.GetIPProperties().GatewayAddresses)
                {
                    if (gw.Address != null && gw.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        gateway = gw.Address.ToString();
                        break;
                    }
                }
                foreach (var uni in nic.GetIPProperties().UnicastAddresses)
                {
                    var ip = uni.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ip)) continue;
                    var text = ip.ToString();
                    if (!ips.Contains(text)) ips.Add(text);
                    ifaces.Add("{\"address\":\"" + text + "\",\"name\":\"" + name + "\",\"gateway\":\"" + Escape(gateway) + "\"}");
                }
            }
            WriteMessage("{\"ips\":[" + JoinQuoted(ips) + "],\"ifaces\":[" + string.Join(",", ifaces.ToArray()) + "]}");
            return 0;
        }
        catch
        {
            WriteMessage("{\"ips\":[],\"ifaces\":[]}");
            return 0;
        }
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static void ReadIgnoredRequest()
    {
        var stdin = Console.OpenStandardInput();
        var header = new byte[4];
        var read = stdin.Read(header, 0, 4);
        if (read < 4) return;
        var len = BitConverter.ToInt32(header, 0);
        if (len <= 0 || len > 1000000) return;
        var body = new byte[len];
        var offset = 0;
        while (offset < len)
        {
            var n = stdin.Read(body, offset, len - offset);
            if (n <= 0) break;
            offset += n;
        }
    }

    private static void WriteMessage(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var stdout = Console.OpenStandardOutput();
        stdout.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();
    }

    private static string JoinQuoted(List<string> ips)
    {
        var parts = new string[ips.Count];
        for (var i = 0; i < ips.Count; i++)
        {
            parts[i] = "\"" + ips[i] + "\"";
        }
        return string.Join(",", parts);
    }
}
