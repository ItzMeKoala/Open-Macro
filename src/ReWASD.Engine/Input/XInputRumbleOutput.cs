using System.Runtime.InteropServices;

namespace ReWASD.Engine.Input;

/// <summary>Vibrates the physical XInput controller that <see cref="XInputReader"/> is currently reading.</summary>
public sealed class XInputRumbleOutput : IRumbleOutput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputSetState(uint userIndex, ref XInputVibration vibration);

    private readonly XInputReader _reader;

    public XInputRumbleOutput(XInputReader reader)
    {
        _reader = reader;
    }

    public void Rumble(double left, double right)
    {
        int index = _reader.ActiveUserIndex;
        if (index < 0) return; // no physical controller to vibrate

        var vibration = new XInputVibration
        {
            wLeftMotorSpeed = (ushort)(Math.Clamp(left, 0, 1) * ushort.MaxValue),
            wRightMotorSpeed = (ushort)(Math.Clamp(right, 0, 1) * ushort.MaxValue)
        };

        try
        {
            XInputSetState((uint)index, ref vibration);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // XInput unavailable: nothing to vibrate
        }
    }
}
