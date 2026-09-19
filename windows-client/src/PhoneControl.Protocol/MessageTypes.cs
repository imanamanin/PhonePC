namespace PhoneControl.Protocol;

public static class MessageTypes
{
    public const string SessionHello = "session.hello";
    public const string SessionHelloAck = "session.hello_ack";
    public const string SessionPing = "session.ping";
    public const string SessionPong = "session.pong";
    public const string SessionHeartbeat = "session.heartbeat";
    public const string SessionClose = "session.close";
    public const string SessionReconnect = "session.reconnect";

    public const string PairingStart = "pairing.start";
    public const string PairingSubmit = "pairing.submit";
    public const string PairingAccepted = "pairing.accepted";
    public const string PairingRejected = "pairing.rejected";
    public const string PairingExpired = "pairing.expired";
    public const string PairingCleared = "pairing.cleared";
    public const string PairingConfirm = "pairing.confirm";

    public const string CapabilitiesGet = "capabilities.get";
    public const string CapabilitiesStatus = "capabilities.status";
    public const string PermissionsGet = "permissions.get";
    public const string PermissionsStatus = "permissions.status";
    public const string DeviceStatus = "device.status";
    public const string StateUpdate = "state.update";

    public const string ScreenStart = "screen.start";
    public const string ScreenStop = "screen.stop";
    public const string ScreenMetadata = "screen.metadata";

    public const string VideoStart = "video.start";
    public const string VideoStop = "video.stop";
    public const string VideoAdapt = "video.adapt";
    public const string VideoFrame = "video.frame";

    public const string InputTouch = "input.touch";
    public const string InputKey = "input.key";
    public const string InputPinch = "input.pinch";
    public const string InputScreenshot = "input.screenshot";

    public const string NotificationReceived = "notification.received";
    public const string NotificationRemoved = "notification.removed";
    public const string NotificationUpdated = "notification.updated";
    public const string NotificationAction = "notification.action";

    public const string ClipboardChanged = "clipboard.changed";
    public const string ClipboardSet = "clipboard.set";

    public const string SmsList = "sms.list";
    public const string SmsReceived = "sms.received";
    public const string SmsSend = "sms.send";

    public const string FileOffer = "file.offer";
    public const string FileChunk = "file.chunk";
    public const string FileProgress = "file.progress";
    public const string FileCancel = "file.cancel";
    public const string FileComplete = "file.complete";
    public const string FileError = "file.error";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SessionHello, SessionHelloAck, SessionPing, SessionPong, SessionHeartbeat, SessionClose, SessionReconnect,
        PairingStart, PairingSubmit, PairingAccepted, PairingRejected, PairingExpired, PairingCleared, PairingConfirm,
        CapabilitiesGet, CapabilitiesStatus, PermissionsGet, PermissionsStatus, DeviceStatus, StateUpdate,
        ScreenStart, ScreenStop, ScreenMetadata,
        VideoStart, VideoStop, VideoAdapt, VideoFrame,
        InputTouch, InputKey, InputPinch, InputScreenshot,
        NotificationReceived, NotificationRemoved, NotificationUpdated, NotificationAction,
        ClipboardChanged, ClipboardSet,
        SmsList, SmsReceived, SmsSend,
        FileOffer, FileChunk, FileProgress, FileCancel, FileComplete, FileError
    };

    public static bool IsKnown(string type) => All.Contains(type);
}
