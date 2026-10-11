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
            _virtualXbox = null;
        }
        catch (Exception)
        {
            // Ignore on disconnect
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
            _virtualXbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)Math.Clamp((int)(state.LeftTrigger * 255), 0, 255));
            _virtualXbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)Math.Clamp((int)(state.RightTrigger * 255), 0, 255));

            // Set Sticks (short -32768..32767)
            _virtualXbox.SetAxisValue(Xbox360Axis.LeftThumbX, (short)Math.Clamp((int)(state.LeftStickX * 32767), -32768, 32767));
            _virtualXbox.SetAxisValue(Xbox360Axis.LeftThumbY, (short)Math.Clamp((int)(state.LeftStickY * 32767), -32768, 32767));
            _virtualXbox.SetAxisValue(Xbox360Axis.RightThumbX, (short)Math.Clamp((int)(state.RightStickX * 32767), -32768, 32767));
            _virtualXbox.SetAxisValue(Xbox360Axis.RightThumbY, (short)Math.Clamp((int)(state.RightStickY * 32767), -32768, 32767));

            _virtualXbox.SubmitReport();
        }
        catch (Exception)
        {
            // Suppress transmission errors
        }
    }

    public void Dispose()
    {
        DisconnectVirtualXbox();
        _client?.Dispose();
    }
}
