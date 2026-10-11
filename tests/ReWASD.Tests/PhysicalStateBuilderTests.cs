using ReWASD.Core.Models;
using ReWASD.Engine.Input;

namespace ReWASD.Tests;

public class PhysicalStateBuilderTests
{
    private static PhysicalState Build(double lx = 0, double ly = 0, double rx = 0, double ry = 0, double lt = 0, double rt = 0,
        params ControllerButton[] pressed) =>
        PhysicalStateBuilder.Build(new HashSet<ControllerButton>(pressed), lx, ly, rx, ry, lt, rt);

    [Fact]
    public void DigitalButtons_PassThrough()
    {
        var s = Build(pressed: new[] { ControllerButton.A, ControllerButton.DpadUp });

        Assert.True(s.Buttons.SetEquals(new[] { ControllerButton.A, ControllerButton.DpadUp }));
    }

    [Fact]
    public void Triggers_CountAsPressedPastTheThreshold()
    {
        Assert.Empty(Build(lt: 0.35, rt: 0.2).Buttons);
        var s = Build(lt: 0.36, rt: 1.0);
        Assert.True(s.Buttons.SetEquals(new[] { ControllerButton.LeftTrigger, ControllerButton.RightTrigger }));
        Assert.Equal(0.36, s.LeftTrigger);
    }

    [Fact]
    public void Sticks_BecomeDirectionsPastHalfway_WithUpPositive()
    {
        Assert.Empty(Build(lx: 0.5, ly: -0.5, rx: -0.5, ry: 0.5).Buttons);

        var s = Build(lx: -0.8, ly: 0.9, rx: 0.7, ry: -0.6);
        Assert.True(s.Buttons.SetEquals(new[]
        {
            ControllerButton.LeftStickLeft, ControllerButton.LeftStickUp,
            ControllerButton.RightStickRight, ControllerButton.RightStickDown
        }));
        Assert.Equal(0.9, s.LeftStickY);
    }
}
