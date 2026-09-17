namespace PhoneControl.Domain;

public sealed record DeviceIdentity(
    string Model,
    string Manufacturer,
    string AndroidVersion,
    int SdkInt,
    string SerialHash);

public sealed record DeviceStatus(
    int? BatteryPercent,
    bool? Charging,
    NetworkKind Network,
    DateTimeOffset? DeviceTimeUtc,
    int? ScreenWidth,
    int? ScreenHeight,
    int Rotation,
    long? StorageFreeBytes,
    bool? AccessibilityGranted = null,
    bool? CaptureGranted = null);

public sealed record PermissionSnapshot(
    PermissionState Accessibility,
    PermissionState NotificationListener,
    PermissionState MediaProjection,
    PermissionState SmsRead,
    PermissionState SmsSend,
    PermissionState PostNotifications);

public sealed record ConnectionSnapshot(
    ConnectionState State,
    DeviceIdentity? Device,
    bool UsbPresent,
    bool AdbAvailable,
    bool TetheringLikely,
    string? Endpoint,
    string? ErrorCode,
    IReadOnlyList<string> DiscoveredEndpoints,
    DeviceStatus? Status = null,
    string? Sas = null,
    bool TrustedDevice = false)
{
    public static ConnectionSnapshot Idle() =>
        new(
            ConnectionState.Disconnected,
            null,
            false,
            false,
            false,
            null,
            null,
            Array.Empty<string>());
}
