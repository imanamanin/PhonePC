namespace PhoneControl.Domain;

public enum ConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    PairingRequired = 2,
    Connected = 3,
    Reconnecting = 4,
    Error = 5
}

public enum PermissionState
{
    Unknown = 0,
    Granted = 1,
    Denied = 2,
    Unsupported = 3
}

public enum NetworkKind
{
    Unknown = 0,
    UsbTether = 1,
    Wifi = 2,
    Cellular = 3,
    None = 4
}
