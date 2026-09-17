using PhoneControl.Protocol;

namespace PhoneControl.Input;

/// <summary>
/// Win32 INPUT layout used by SendInput. Remote control maps these records to
/// protocol messages instead of injecting into the local desktop.
/// </summary>
public enum Win32InputKind
{
    MouseMove,
    MouseLeftDown,
    MouseLeftUp,
    MouseWheel,
    MouseRightDown,
    MouseRightUp,
    KeyDown,
    KeyUp,
    TextInput,
    Pinch
}

public readonly record struct Win32InputEvent(
    Win32InputKind Kind,
    int X,
    int Y,
    int Data,
    double Scale,
    string? Text = null);

public static class Win32InputMap
{
    public const int VkEscape = 0x1B;
    public const int VkHome = 0x24;
    public const int VkReturn = 0x0D;
    public const int VkBack = 0x08;

    public static MessageEnvelope ToEnvelope(Win32InputEvent input, PhonePoint? point, long timestamp)
    {
        return input.Kind switch
        {
            Win32InputKind.MouseLeftDown => Touch("down", point, timestamp),
            Win32InputKind.MouseLeftUp => Touch("up", point, timestamp),
            Win32InputKind.MouseMove => Touch("move", point, timestamp),
            Win32InputKind.MouseWheel => Scroll(point, input.Data, timestamp),
            Win32InputKind.Pinch => EnvelopeFactory.Create(
                MessageTypes.InputPinch,
                timestamp,
                new InputPinchPayload
                {
                    X = point?.X ?? 0,
                    Y = point?.Y ?? 0,
                    X2 = point?.X ?? 0,
                    Y2 = (point?.Y ?? 0) + input.Data,
                    Scale = input.Scale
                }),
            Win32InputKind.KeyDown => EnvelopeFactory.Create(
                MessageTypes.InputKey,
                timestamp,
                new InputKeyPayload
                {
                    Action = string.IsNullOrEmpty(input.Text) ? "click" : "type",
                    Key = string.IsNullOrEmpty(input.Text) ? MapKey(input.Data) : "type",
                    Text = input.Text
                }),
            Win32InputKind.TextInput => EnvelopeFactory.Create(
                MessageTypes.InputKey,
                timestamp,
                new InputKeyPayload { Action = "type", Key = "type", Text = input.Text ?? string.Empty }),
            _ => EnvelopeFactory.Create(
                MessageTypes.InputKey,
                timestamp,
                new InputKeyPayload { Action = "up", Key = MapKey(input.Data) })
        };
    }

    public static string MapKey(int virtualKey) =>
        virtualKey switch
        {
            VkEscape => "back",
            VkHome => "home",
            VkReturn => "enter",
            VkBack => "backspace",
            0x2E => "delete",
            _ => "text"
        };

    private static MessageEnvelope Touch(string action, PhonePoint? point, long timestamp)
    {
        return EnvelopeFactory.Create(
            MessageTypes.InputTouch,
            timestamp,
            new InputTouchPayload
            {
                Action = action,
                X = point?.X ?? 0,
                Y = point?.Y ?? 0
            });
    }

    private static MessageEnvelope Scroll(PhonePoint? point, int wheelDelta, long timestamp)
    {
        return EnvelopeFactory.Create(
            MessageTypes.InputTouch,
            timestamp,
            new InputTouchPayload
            {
                Action = "scroll",
                X = point?.X ?? 0,
                Y = point?.Y ?? 0,
                Y2 = (point?.Y ?? 0) - wheelDelta,
                Delta = wheelDelta
            });
    }
}

public static class NativeSendInput
{
    public const uint InputMouse = 0;
    public const uint InputKeyboard = 1;
    public const uint MouseEventMove = 0x0001;
    public const uint MouseEventLeftDown = 0x0002;
    public const uint MouseEventLeftUp = 0x0004;
    public const uint MouseEventRightDown = 0x0008;
    public const uint MouseEventRightUp = 0x0010;
    public const uint MouseEventWheel = 0x0800;
    public const uint KeyEventKeyUp = 0x0002;

    /// <summary>
    /// Packs a SendInput-compatible mouse/keyboard record. Production code sends
    /// this over TCP rather than calling user32 SendInput against the local session.
    /// </summary>
    public static Win32InputEvent FromMouse(int x, int y, uint flags, int data = 0)
    {
        var kind = flags switch
        {
            MouseEventLeftDown => Win32InputKind.MouseLeftDown,
            MouseEventLeftUp => Win32InputKind.MouseLeftUp,
            MouseEventRightDown => Win32InputKind.MouseRightDown,
            MouseEventRightUp => Win32InputKind.MouseRightUp,
            MouseEventWheel => Win32InputKind.MouseWheel,
            _ => Win32InputKind.MouseMove
        };
        return new Win32InputEvent(kind, x, y, data, 1);
    }
}
