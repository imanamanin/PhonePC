namespace PhoneControl.Protocol;

public sealed class PairingSubmitPayload
{
    public string Pin { get; init; } = string.Empty;
}

public sealed class PairingAcceptedPayload
{
    public string PairingId { get; init; } = string.Empty;
    public string SessionToken { get; init; } = string.Empty;
    public long ExpiresAt { get; init; }
    public string Sas { get; init; } = string.Empty;
}

public sealed class PairingRejectedPayload
{
    public string Reason { get; init; } = "pin_mismatch";
}

public sealed class PairingConfirmPayload
{
    public string Sas { get; init; } = string.Empty;
}

public sealed class DeviceStatusPayload
{
    public int? BatteryPercent { get; init; }
    public bool? Charging { get; init; }
    public string Network { get; init; } = "unknown";
    public long DeviceTimeUtc { get; init; }
    public int? ScreenWidth { get; init; }
    public int? ScreenHeight { get; init; }
    public int Rotation { get; init; }
    public bool? Accessibility { get; init; }
    public bool? MediaProjection { get; init; }
}
