using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.VirtualOutput;

public class VirtualGamepadService : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _virtualXbox;
    private bool _isViGEmAvailable = false;

    public bool IsDriverAvailable => _isViGEmAvailable;
    public bool IsConnected => _virtualXbox != null;

    public void Initialize()
    {
        try
        {
            _client = new ViGEmClient();
            _isViGEmAvailable = true;
        }
        catch (Exception)
        {
            // ViGEmBus driver not installed on host machine
            _isViGEmAvailable = false;
        }
    }

    public bool ConnectVirtualXbox()
    {
        if (!_isViGEmAvailable || _client == null) return false;

        try
        {
            _virtualXbox = _client.CreateXbox360Controller();
            _virtualXbox.Connect();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void DisconnectVirtualXbox()
    {
        try
        {
            _virtualXbox?.Disconnect();
        }
        catch (Exception)
        {
            // Ignore on disconnect
        }
        finally
        {
            _virtualXbox = null;
        }
    }

    public void SendReport(RemappedState state)
    {
        if (_virtualXbox == null) return;

        try
        {
            // Reset buttons
            _virtualXbox.SetButtonState(Xbox360Button.A, state.ActiveVirtualButtons.Contains(ControllerButton.A));
            _virtualXbox.SetButtonState(Xbox360Button.B, state.ActiveVirtualButtons.Contains(ControllerButton.B));
            _virtualXbox.SetButtonState(Xbox360Button.X, state.ActiveVirtualButtons.Contains(ControllerButton.X));
            _virtualXbox.SetButtonState(Xbox360Button.Y, state.ActiveVirtualButtons.Contains(ControllerButton.Y));

            _virtualXbox.SetButtonState(Xbox360Button.LeftShoulder, state.ActiveVirtualButtons.Contains(ControllerButton.LeftBumper));
            _virtualXbox.SetButtonState(Xbox360Button.RightShoulder, state.ActiveVirtualButtons.Contains(ControllerButton.RightBumper));

            _virtualXbox.SetButtonState(Xbox360Button.LeftThumb, state.ActiveVirtualButtons.Contains(ControllerButton.LeftStickClick));
            _virtualXbox.SetButtonState(Xbox360Button.RightThumb, state.ActiveVirtualButtons.Contains(ControllerButton.RightStickClick));

            _virtualXbox.SetButtonState(Xbox360Button.Start, state.ActiveVirtualButtons.Contains(ControllerButton.Menu));
            _virtualXbox.SetButtonState(Xbox360Button.Back, state.ActiveVirtualButtons.Contains(ControllerButton.View));
            _virtualXbox.SetButtonState(Xbox360Button.Guide, state.ActiveVirtualButtons.Contains(ControllerButton.Guide));

            _virtualXbox.SetButtonState(Xbox360Button.Up, state.ActiveVirtualButtons.Contains(ControllerButton.DpadUp));
            _virtualXbox.SetButtonState(Xbox360Button.Down, state.ActiveVirtualButtons.Contains(ControllerButton.DpadDown));
            _virtualXbox.SetButtonState(Xbox360Button.Left, state.ActiveVirtualButtons.Contains(ControllerButton.DpadLeft));
            _virtualXbox.SetButtonState(Xbox360Button.Right, state.ActiveVirtualButtons.Contains(ControllerButton.DpadRight));

            // Set Triggers (byte 0..255)
            // A button remapped onto a trigger counts as a full pull
            double lt = state.ActiveVirtualButtons.Contains(ControllerButton.LeftTrigger) ? 1.0 : state.LeftTrigger;
            double rt = state.ActiveVirtualButtons.Contains(ControllerButton.RightTrigger) ? 1.0 : state.RightTrigger;
            _virtualXbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)Math.Clamp((int)Math.Round(lt * 255), 0, 255));
            _virtualXbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)Math.Clamp((int)Math.Round(rt * 255), 0, 255));

            // Set Sticks (short -32768..32767)
            // A stick-direction button remapped onto the virtual stick pushes it fully that way
            var pressed = state.ActiveVirtualButtons;
            double lx = DirectionOverride(pressed, ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight, state.LeftStickX);
            double ly = DirectionOverride(pressed, ControllerButton.LeftStickDown, ControllerButton.LeftStickUp, state.LeftStickY);
            double rx = DirectionOverride(pressed, ControllerButton.RightStickLeft, ControllerButton.RightStickRight, state.RightStickX);
            double ry = DirectionOverride(pressed, ControllerButton.RightStickDown, ControllerButton.RightStickUp, state.RightStickY);

            _virtualXbox.SetAxisValue(Xbox360Axis.LeftThumbX, ToAxis(lx));
            _virtualXbox.SetAxisValue(Xbox360Axis.LeftThumbY, ToAxis(ly));
            _virtualXbox.SetAxisValue(Xbox360Axis.RightThumbX, ToAxis(rx));
            _virtualXbox.SetAxisValue(Xbox360Axis.RightThumbY, ToAxis(ry));

            _virtualXbox.SubmitReport();
        }
        catch (Exception)
        {
            // Suppress transmission errors
        }
    }

    private static double DirectionOverride(HashSet<ControllerButton> pressed, ControllerButton negative, ControllerButton positive, double analog)
    {
        bool neg = pressed.Contains(negative), pos = pressed.Contains(positive);
        if (neg == pos) return analog; // neither, or both cancelling out
        return pos ? 1.0 : -1.0;
    }

    private static short ToAxis(double value) =>
        (short)Math.Clamp((int)Math.Round(value * 32767), -32768, 32767);

    public void Dispose()
    {
        DisconnectVirtualXbox();
        _client?.Dispose();
    }
}
