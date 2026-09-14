namespace PhoneControl.Protocol;

public sealed class VideoStartPayload
{
    public string Codec { get; init; } = "h264";
    public int MaxFps { get; init; } = 30;
    public int MaxWidth { get; init; } = 1280;
    public int BitrateKbps { get; init; } = 4000;
    public int Port { get; init; } = 17891;
}

public sealed class VideoAdaptPayload
{
    public int MaxFps { get; init; }
    public int MaxWidth { get; init; }
    public int BitrateKbps { get; init; }
}

public sealed class VideoFramePayload
{
    public long CaptureTimestamp { get; init; }
    public int ByteLength { get; init; }
    public bool Keyframe { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

public sealed class InputTouchPayload
{
    public string Action { get; init; } = "tap";
    public double X { get; init; }
    public double Y { get; init; }
    public double? X2 { get; init; }
    public double? Y2 { get; init; }
    public int PointerId { get; init; }
    public int DurationMs { get; init; }
    public double? Delta { get; init; }
}

public sealed class InputKeyPayload
{
    public string Action { get; init; } = "click";
    public string Key { get; init; } = "enter";
    public string? Text { get; init; }
}

public sealed class InputPinchPayload
{
    public double X { get; init; }
    public double Y { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public double Scale { get; init; } = 1;
}
