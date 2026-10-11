using System.Runtime.InteropServices;

namespace ReWASD.Engine.Input;

/// <summary>Injects events with SendInput. Keys are sent as scan codes so games using raw input see them.</summary>
public sealed class Win32InputBackend : IInputBackend
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;

    private const uint MouseLeftDown = 0x0002, MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008, MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020, MouseMiddleUp = 0x0040;
    private const uint MouseXDown = 0x0080, MouseXUp = 0x0100;
    private const uint MouseWheelFlag = 0x0800;
    private const uint XButton1 = 1, XButton2 = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint mapType);

    public void KeyDown(ushort virtualKey) => SendKey(virtualKey, false);
    public void KeyUp(ushort virtualKey) => SendKey(virtualKey, true);

    private static void SendKey(ushort vk, bool up)
    {
        uint flags = KeyEventScanCode | (up ? KeyEventKeyUp : 0) | (KeyMap.IsExtended(vk) ? KeyEventExtendedKey : 0);
        var input = new Input
        {
            type = InputKeyboard,
            u = new InputUnion { ki = new KeyboardInput { wVk = 0, wScan = (ushort)MapVirtualKeyW(vk, 0), dwFlags = flags } }
        };
        Send(input);
    }

    public void MouseDown(MouseButtonKind button) => SendMouse(button, true);
    public void MouseUp(MouseButtonKind button) => SendMouse(button, false);

    private static void SendMouse(MouseButtonKind button, bool down)
    {
        (uint flags, uint data) = button switch
        {
            MouseButtonKind.Left => (down ? MouseLeftDown : MouseLeftUp, 0u),
            MouseButtonKind.Right => (down ? MouseRightDown : MouseRightUp, 0u),
            MouseButtonKind.Middle => (down ? MouseMiddleDown : MouseMiddleUp, 0u),
            MouseButtonKind.X1 => (down ? MouseXDown : MouseXUp, XButton1),
            _ => (down ? MouseXDown : MouseXUp, XButton2)
        };
        Send(new Input { type = InputMouse, u = new InputUnion { mi = new MouseInput { dwFlags = flags, mouseData = data } } });
    }

    public void MouseWheel(int delta) =>
        Send(new Input { type = InputMouse, u = new InputUnion { mi = new MouseInput { dwFlags = MouseWheelFlag, mouseData = unchecked((uint)delta) } } });

    private static void Send(Input input) => SendInput(1, new[] { input }, Marshal.SizeOf<Input>());
}
