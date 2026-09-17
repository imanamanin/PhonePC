namespace PhoneControl.Domain;

public static class ProtocolPorts
{
    public const int Control = 17890;
    public const int Screen = 17891;
    public const int Audio = 17892;
    public const int ProtocolVersion = 1;
    public const int HeartbeatIntervalMs = 5000;
    public const int DefaultTimeoutMs = 15000;
    public const int MaxJsonFrameBytes = 1_048_576;
    public const int MaxVideoFrameBytes = 8_388_608;
    public const string FallbackHost = "192.168.42.129";
}

public static class KnownAndroidTetherGateways
{
    /// <summary>Default Android USB tethering IPv4 for the phone itself.</summary>
    public static readonly IReadOnlyList<string> Candidates = new[]
    {
        "192.168.42.129",
        "192.168.137.1"
    };
}
