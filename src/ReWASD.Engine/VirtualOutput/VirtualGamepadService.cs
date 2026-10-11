using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;

namespace ReWASD.Engine.VirtualOutput;

/// <summary>Presents a virtual Xbox 360 or DualShock 4 controller to the system through the ViGEmBus driver.</summary>
public class VirtualGamepadService : IDisposable
{
    /// <summary>The pad types ViGEm can emulate. Xbox One, DualSense and Switch Pro pads are not available through it.</summary>
    public static readonly IReadOnlyList<ControllerType> SupportedTypes = new[] { ControllerType.Xbox360, ControllerType.DualShock4 };

    private ViGEmClient? _client;
    private IXbox360Controller? _xbox;
    private IDualShock4Controller? _ds4;
    private bool _isViGEmAvailable = false;

    public bool IsDriverAvailable => _isViGEmAvailable;
    public bool IsConnected => _xbox != null || _ds4 != null;

    /// <summary>The type currently connected, or null.</summary>
    public ControllerType? ConnectedType => _xbox != null ? ControllerType.Xbox360 : _ds4 != null ? ControllerType.DualShock4 : null;

    public static bool IsSupported(ControllerType type) => SupportedTypes.Contains(type);

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

    /// <summary>Connects a virtual pad of the given type, replacing any that is connected. Returns false if it could not.</summary>
    public bool Connect(ControllerType type)
    {
        DisconnectVirtual();
        if (!_isViGEmAvailable || _client == null || !IsSupported(type)) return false;

        try
        {
            if (type == ControllerType.DualShock4)
            {
                _ds4 = _client.CreateDualShock4Controller();
                _ds4.Connect();
            }
            else
            {
                _xbox = _client.CreateXbox360Controller();
                _xbox.Connect();
            }
            return true;
        }
        catch (Exception)
        {
            _xbox = null;
            _ds4 = null;
            return false;
        }
    }

    public bool ConnectVirtualXbox() => Connect(ControllerType.Xbox360);

    public void DisconnectVirtual()
    {
        try
        {
            _xbox?.Disconnect();
            _ds4?.Disconnect();
        }
        catch (Exception)
        {
            // Ignore on disconnect
        }
        finally
        {
            _xbox = null;
            _ds4 = null;
        }
    }

    public void DisconnectVirtualXbox() => DisconnectVirtual();

    public void SendReport(RemappedState state)
    {
        try
        {
            if (_xbox != null) SendXbox(_xbox, state);
            else if (_ds4 != null) SendDs4(_ds4, state);
        }
        catch (Exception)
        {
            // Suppress transmission errors
        }
    }

    private static void SendXbox(IXbox360Controller pad, RemappedState state)
    {
        var p = state.ActiveVirtualButtons;

        pad.SetButtonState(Xbox360Button.A, p.Contains(ControllerButton.A));
        pad.SetButtonState(Xbox360Button.B, p.Contains(ControllerButton.B));
        pad.SetButtonState(Xbox360Button.X, p.Contains(ControllerButton.X));
        pad.SetButtonState(Xbox360Button.Y, p.Contains(ControllerButton.Y));

        pad.SetButtonState(Xbox360Button.LeftShoulder, p.Contains(ControllerButton.LeftBumper));
        pad.SetButtonState(Xbox360Button.RightShoulder, p.Contains(ControllerButton.RightBumper));

        pad.SetButtonState(Xbox360Button.LeftThumb, p.Contains(ControllerButton.LeftStickClick));
        pad.SetButtonState(Xbox360Button.RightThumb, p.Contains(ControllerButton.RightStickClick));

        pad.SetButtonState(Xbox360Button.Start, p.Contains(ControllerButton.Menu));
        pad.SetButtonState(Xbox360Button.Back, p.Contains(ControllerButton.View));
        pad.SetButtonState(Xbox360Button.Guide, p.Contains(ControllerButton.Guide));

        pad.SetButtonState(Xbox360Button.Up, p.Contains(ControllerButton.DpadUp));
        pad.SetButtonState(Xbox360Button.Down, p.Contains(ControllerButton.DpadDown));
        pad.SetButtonState(Xbox360Button.Left, p.Contains(ControllerButton.DpadLeft));
        pad.SetButtonState(Xbox360Button.Right, p.Contains(ControllerButton.DpadRight));

        var axes = VirtualPadMath.ResolveAxes(state);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, VirtualPadMath.ToByte(axes.LeftTrigger));
        pad.SetSliderValue(Xbox360Slider.RightTrigger, VirtualPadMath.ToByte(axes.RightTrigger));

        pad.SetAxisValue(Xbox360Axis.LeftThumbX, VirtualPadMath.ToXboxAxis(axes.LeftX));
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, VirtualPadMath.ToXboxAxis(axes.LeftY));
        pad.SetAxisValue(Xbox360Axis.RightThumbX, VirtualPadMath.ToXboxAxis(axes.RightX));
        pad.SetAxisValue(Xbox360Axis.RightThumbY, VirtualPadMath.ToXboxAxis(axes.RightY));

        pad.SubmitReport();
    }

    private static readonly DualShock4Button[] AllDs4Buttons =
    {
        DualShock4Button.Cross, DualShock4Button.Circle, DualShock4Button.Square, DualShock4Button.Triangle,
        DualShock4Button.ShoulderLeft, DualShock4Button.ShoulderRight,
        DualShock4Button.ThumbLeft, DualShock4Button.ThumbRight,
        DualShock4Button.Share, DualShock4Button.Options,
        DualShock4Button.TriggerLeft, DualShock4Button.TriggerRight
    };

    private static void SendDs4(IDualShock4Controller pad, RemappedState state)
    {
        var frame = VirtualPadMath.ToDs4(state);

        foreach (var button in AllDs4Buttons) pad.SetButtonState(button, frame.Buttons.Contains(button));
        pad.SetButtonState(DualShock4SpecialButton.Ps, frame.Ps);
        pad.SetDPadDirection(frame.DPad);

        pad.SetAxisValue(DualShock4Axis.LeftThumbX, frame.LeftX);
        pad.SetAxisValue(DualShock4Axis.LeftThumbY, frame.LeftY);
        pad.SetAxisValue(DualShock4Axis.RightThumbX, frame.RightX);
        pad.SetAxisValue(DualShock4Axis.RightThumbY, frame.RightY);
        pad.SetSliderValue(DualShock4Slider.LeftTrigger, frame.LeftTrigger);
        pad.SetSliderValue(DualShock4Slider.RightTrigger, frame.RightTrigger);

        pad.SubmitReport();
    }

    public void Dispose()
    {
        DisconnectVirtual();
        _client?.Dispose();
    }
}
