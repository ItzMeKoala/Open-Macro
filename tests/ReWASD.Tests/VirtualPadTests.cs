using Nefarius.ViGEm.Client.Targets.DualShock4;
using ReWASD.Core.Models;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.Tests;

public class VirtualPadTests
{
    private static RemappedState State(params ControllerButton[] pressed)
    {
        var s = new RemappedState();
        foreach (var b in pressed) s.ActiveVirtualButtons.Add(b);
        return s;
    }

    [Fact]
    public void OnlyXbox360AndDualShock4_AreSupported()
    {
        Assert.True(VirtualGamepadService.IsSupported(ControllerType.Xbox360));
        Assert.True(VirtualGamepadService.IsSupported(ControllerType.DualShock4));
        Assert.False(VirtualGamepadService.IsSupported(ControllerType.XboxOne));
        Assert.False(VirtualGamepadService.IsSupported(ControllerType.DualSense));
        Assert.False(VirtualGamepadService.IsSupported(ControllerType.SwitchPro));
    }

    [Fact]
    public void FaceButtons_MapToPlayStationNames()
    {
        var frame = VirtualPadMath.ToDs4(State(ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y));

        Assert.True(frame.Buttons.SetEquals(new[] { DualShock4Button.Cross, DualShock4Button.Circle, DualShock4Button.Square, DualShock4Button.Triangle }));
    }

    [Fact]
    public void ShouldersSticksAndMenuButtons_Map()
    {
        var frame = VirtualPadMath.ToDs4(State(
            ControllerButton.LeftBumper, ControllerButton.RightBumper,
            ControllerButton.LeftStickClick, ControllerButton.RightStickClick,
            ControllerButton.View, ControllerButton.Menu, ControllerButton.Guide));

        Assert.True(frame.Buttons.SetEquals(new[]
        {
            DualShock4Button.ShoulderLeft, DualShock4Button.ShoulderRight, DualShock4Button.ThumbLeft,
            DualShock4Button.ThumbRight, DualShock4Button.Share, DualShock4Button.Options
        }));
        Assert.True(frame.Ps);
    }

    private static DualShock4DPadDirection Direction(string name) => name switch
    {
        "North" => DualShock4DPadDirection.North,
        "Northeast" => DualShock4DPadDirection.Northeast,
        "East" => DualShock4DPadDirection.East,
        "Southeast" => DualShock4DPadDirection.Southeast,
        "South" => DualShock4DPadDirection.South,
        "Southwest" => DualShock4DPadDirection.Southwest,
        "West" => DualShock4DPadDirection.West,
        "Northwest" => DualShock4DPadDirection.Northwest,
        _ => DualShock4DPadDirection.None
    };

    [Theory]
    [InlineData(true, false, false, false, "North")]
    [InlineData(true, true, false, false, "Northeast")]
    [InlineData(false, true, true, false, "Southeast")]
    [InlineData(false, false, true, true, "Southwest")]
    [InlineData(true, false, false, true, "Northwest")]
    [InlineData(false, false, false, true, "West")]
    [InlineData(false, false, false, false, "None")]
    [InlineData(true, false, true, false, "None")]   // up + down cancel
    [InlineData(true, true, true, true, "None")]     // everything cancels
    [InlineData(true, false, true, true, "West")]    // up/down cancel, left remains
    public void DPad_BecomesOneOfEightDirections(bool up, bool right, bool down, bool left, string expected)
    {
        Assert.Same(Direction(expected), VirtualPadMath.DPad(up, right, down, left));
    }

    [Fact]
    public void Ds4Sticks_CentreIs128_AndUpIsZero()
    {
        var centred = VirtualPadMath.ToDs4(new RemappedState());
        Assert.Equal(128, centred.LeftX);
        Assert.Equal(128, centred.LeftY);

        var up = VirtualPadMath.ToDs4(new RemappedState { LeftStickY = 1.0, RightStickX = 1.0 });
        Assert.Equal(0, up.LeftY);       // a DualShock reports up as the smallest value
        Assert.Equal(255, up.RightX);

        var down = VirtualPadMath.ToDs4(new RemappedState { LeftStickY = -1.0 });
        Assert.Equal(255, down.LeftY);
    }

    [Fact]
    public void Ds4Triggers_AlsoPressTheDigitalButtonPastTheThreshold()
    {
        Assert.DoesNotContain(DualShock4Button.TriggerLeft, VirtualPadMath.ToDs4(new RemappedState { LeftTrigger = 0.2 }).Buttons);

        var pulled = VirtualPadMath.ToDs4(new RemappedState { LeftTrigger = 0.6, RightTrigger = 1.0 });
        Assert.Contains(DualShock4Button.TriggerLeft, pulled.Buttons);
        Assert.Contains(DualShock4Button.TriggerRight, pulled.Buttons);
        Assert.Equal(153, pulled.LeftTrigger);
        Assert.Equal(255, pulled.RightTrigger);
    }

    [Fact]
    public void ButtonsRemappedOntoTriggersAndStickDirections_DriveTheAnalogValues()
    {
        var axes = VirtualPadMath.ResolveAxes(State(ControllerButton.RightTrigger, ControllerButton.LeftStickUp, ControllerButton.RightStickLeft));

        Assert.Equal(1.0, axes.RightTrigger);
        Assert.Equal(1.0, axes.LeftY);
        Assert.Equal(-1.0, axes.RightX);
        Assert.Equal(0.0, axes.LeftTrigger);
    }

    [Fact]
    public void OppositeStickDirections_CancelBackToTheAnalogValue()
    {
        var state = State(ControllerButton.LeftStickLeft, ControllerButton.LeftStickRight);
        state.LeftStickX = 0.25;

        Assert.Equal(0.25, VirtualPadMath.ResolveAxes(state).LeftX);
    }

    [Fact]
    public void XboxAxisConversion_ClampsToTheShortRange()
    {
        Assert.Equal(32767, VirtualPadMath.ToXboxAxis(1.0));
        Assert.Equal(-32767, VirtualPadMath.ToXboxAxis(-1.0));
        Assert.Equal(32767, VirtualPadMath.ToXboxAxis(5.0));
        Assert.Equal(-32768, VirtualPadMath.ToXboxAxis(-5.0));
        Assert.Equal(0, VirtualPadMath.ToXboxAxis(0));
    }
}
