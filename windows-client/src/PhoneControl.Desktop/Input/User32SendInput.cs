using System.Runtime.InteropServices;
using PhoneControl.Input;

namespace PhoneControl.Desktop.Input;

/// <summary>
/// user32 SendInput. Not used to drive the phone — that would inject into this WPF process.
/// Pointer events are translated through <see cref="Win32InputMap"/> onto TCP 17890.
/// </summary>
public static class User32SendInput
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    public static INPUT FromEvent(Win32InputEvent ev)
    {
        if (ev.Kind is Win32InputKind.KeyDown or Win32InputKind.KeyUp)
        {
            return new INPUT
            {
                Type = NativeSendInput.InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KEYBDINPUT
                    {
                        WVk = (ushort)ev.Data,
                        DwFlags = ev.Kind == Win32InputKind.KeyUp ? NativeSendInput.KeyEventKeyUp : 0
                    }
                }
            };
        }

        uint flags = ev.Kind switch
        {
            Win32InputKind.MouseLeftDown => NativeSendInput.MouseEventLeftDown,
            Win32InputKind.MouseLeftUp => NativeSendInput.MouseEventLeftUp,
            Win32InputKind.MouseWheel => NativeSendInput.MouseEventWheel,
            _ => NativeSendInput.MouseEventMove
        };
        return new INPUT
        {
            Type = NativeSendInput.InputMouse,
            Data = new InputUnion
            {
                Mouse = new MOUSEINPUT
                {
                    Dx = ev.X,
                    Dy = ev.Y,
                    MouseData = (uint)ev.Data,
                    DwFlags = flags
                }
            }
        };
    }
}
