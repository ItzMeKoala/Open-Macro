using System.Runtime.InteropServices;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Input;

/// <summary>One snapshot of a physical controller, with sticks in -1..1 and triggers in 0..1.</summary>
public sealed record PhysicalState(
    HashSet<ControllerButton> Buttons,
    double LeftStickX,
    double LeftStickY,
    double RightStickX,
    double RightStickY,
    double LeftTrigger,
    double RightTrigger);

public interface IGamepadReader
{
    PhysicalState? Read();
}

/// <summary>
/// Polls the first connected XInput controller. The virtual ViGEm pad also shows up as an XInput
/// device, so its slot must be excluded or the output would be read back in as input.
/// </summary>
public sealed class XInputReader : IGamepadReader
{
    private const int MaxControllers = 4;
    private const uint ErrorSuccess = 0;

    private const ushort DpadUp = 0x0001, DpadDown = 0x0002, DpadLeft = 0x0004, DpadRight = 0x0008;
    private const ushort Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200, GuideButton = 0x0400;
    private const ushort BtnA = 0x1000, BtnB = 0x2000, BtnX = 0x4000, BtnY = 0x8000;

    private const double TriggerPressThreshold = 0.35;
    private const double StickDirectionThreshold = 0.5;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    // Ordinal 100 is the undocumented XInputGetStateEx, the only variant that reports the Guide button
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern uint XInputGetStateEx(uint userIndex, out XInputState state);

    /// <summary>Slot of the virtual pad to ignore, or -1 for none.</summary>
    public int ExcludedUserIndex { get; set; } = -1;

    /// <summary>Slot currently being read, or -1 if no controller is connected.</summary>
    public int ActiveUserIndex { get; private set; } = -1;

    public static IReadOnlyList<int> GetConnectedSlots()
    {
        var slots = new List<int>();
        for (int i = 0; i < MaxControllers; i++)
        {
            if (TryGetState(i, out _)) slots.Add(i);
        }
        return slots;
    }

    public PhysicalState? Read()
    {
        // Keep the current slot while it stays connected so a second pad can't steal focus mid-game
        if (ActiveUserIndex >= 0 && ActiveUserIndex != ExcludedUserIndex && TryGetState(ActiveUserIndex, out var current))
        {
            return Convert(current);
        }

        ActiveUserIndex = -1;
        for (int i = 0; i < MaxControllers; i++)
        {
            if (i == ExcludedUserIndex) continue;
            if (TryGetState(i, out var state))
            {
                ActiveUserIndex = i;
                return Convert(state);
            }
        }
        return null;
    }

    private static bool TryGetState(int index, out XInputState state)
    {
        try
        {
            return XInputGetStateEx((uint)index, out state) == ErrorSuccess;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            state = default;
            return false;
        }
    }

    private static PhysicalState Convert(XInputState s)
    {
        var g = s.Gamepad;
        var buttons = new HashSet<ControllerButton>();

        void Map(ushort mask, ControllerButton button)
        {
            if ((g.wButtons & mask) != 0) buttons.Add(button);
        }

        Map(DpadUp, ControllerButton.DpadUp);
        Map(DpadDown, ControllerButton.DpadDown);
        Map(DpadLeft, ControllerButton.DpadLeft);
        Map(DpadRight, ControllerButton.DpadRight);
        Map(Start, ControllerButton.Menu);
        Map(Back, ControllerButton.View);
        Map(LeftThumb, ControllerButton.LeftStickClick);
        Map(RightThumb, ControllerButton.RightStickClick);
        Map(LeftShoulder, ControllerButton.LeftBumper);
        Map(RightShoulder, ControllerButton.RightBumper);
        Map(GuideButton, ControllerButton.Guide);
        Map(BtnA, ControllerButton.A);
        Map(BtnB, ControllerButton.B);
        Map(BtnX, ControllerButton.X);
        Map(BtnY, ControllerButton.Y);

        double lx = Axis(g.sThumbLX), ly = Axis(g.sThumbLY);
        double rx = Axis(g.sThumbRX), ry = Axis(g.sThumbRY);
        double lt = g.bLeftTrigger / 255.0, rt = g.bRightTrigger / 255.0;

        // Analog inputs also act as buttons so they can be remapped
        if (lt > TriggerPressThreshold) buttons.Add(ControllerButton.LeftTrigger);
        if (rt > TriggerPressThreshold) buttons.Add(ControllerButton.RightTrigger);
        AddDirections(buttons, lx, ly, ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight, ControllerButton.LeftStickDown, ControllerButton.LeftStickUp);
        AddDirections(buttons, rx, ry, ControllerButton.RightStickLeft, ControllerButton.RightStickRight, ControllerButton.RightStickDown, ControllerButton.RightStickUp);

        return new PhysicalState(buttons, lx, ly, rx, ry, lt, rt);
    }

    private static void AddDirections(HashSet<ControllerButton> buttons, double x, double y,
        ControllerButton left, ControllerButton right, ControllerButton down, ControllerButton up)
    {
        if (x < -StickDirectionThreshold) buttons.Add(left);
        if (x > StickDirectionThreshold) buttons.Add(right);
        if (y < -StickDirectionThreshold) buttons.Add(down);
        if (y > StickDirectionThreshold) buttons.Add(up);
    }

    private static double Axis(short raw) => Math.Clamp(raw / 32767.0, -1.0, 1.0);
}
