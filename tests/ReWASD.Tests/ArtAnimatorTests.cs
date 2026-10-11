using ReWASD.Core.Models;
using ReWASD.Core.Skins.Art;

namespace ReWASD.Tests;

public class ArtAnimatorTests
{
    private static ArtPartAnimator For(ControllerButton button) => new(ArtLibrary.DualSense.PartFor(button)!);

    private static void Run(ArtPartAnimator a, double seconds, double frame = 1.0 / 60)
    {
        for (double t = 0; t < seconds; t += frame) a.Step(frame);
    }

    [Fact]
    public void AnIdlePart_SitsStill()
    {
        var a = For(ControllerButton.A);

        Assert.True(a.IsSettled);
        Assert.Equal(ArtMatrix.Identity, a.Pose.Matrix);
        Assert.Equal(0, a.Pose.Glow);
    }

    [Fact]
    public void APressedButton_SinksAndLights_ThenReturns()
    {
        var a = For(ControllerButton.A);
        var part = a.Part;

        a.SetPressed(true);
        Run(a, 0.5);
        var down = a.Pose;

        Assert.Equal(1, down.Glow, 2);
        // Pressed: smaller than rest and moved down by PressDy
        Assert.Equal(part.PressScale, down.Matrix.A, 2);
        var (_, y) = down.Matrix.Apply(part.Origin.X, part.Origin.Y);
        Assert.Equal(part.Origin.Y + part.PressDy, y, 2);

        a.SetPressed(false);
        Run(a, 0.6);
        Assert.True(a.IsSettled);
        Assert.Equal(ArtMatrix.Identity, a.Pose.Matrix);
        Assert.Equal(0, a.Pose.Glow);
    }

    [Fact]
    public void AQuickTap_StillShowsAsAPress()
    {
        var a = For(ControllerButton.B);

        a.SetPressed(true);
        a.Step(1.0 / 60);
        a.SetPressed(false);          // released before the next frame
        double peak = 0;
        for (int i = 0; i < 30; i++)
        {
            a.Step(1.0 / 60);
            peak = Math.Max(peak, a.Pose.Glow);
        }

        Assert.True(peak > 0.6, $"a 16 ms tap only reached {peak:0.00}");
        Run(a, 1);
        Assert.True(a.IsSettled);
    }

    [Fact]
    public void ATrigger_FollowsHowFarItIsPulled()
    {
        var a = For(ControllerButton.RightTrigger);
        var part = a.Part;

        a.SetAnalog(0.5);
        Run(a, 0.6);
        var half = a.Pose;
        a.SetAnalog(1);
        Run(a, 0.6);
        var full = a.Pose;

        Assert.Equal(0.5, half.Glow, 2);
        Assert.Equal(1, full.Glow, 2);
        Assert.Equal(part.Travel * 0.5, half.Matrix.F - part.Origin.Y * (1 - half.Matrix.D), 1);
        Assert.True(full.Matrix.F > half.Matrix.F);          // pulled further
        Assert.True(full.Matrix.D < 1);                      // and squashed a little
    }

    [Fact]
    public void AStick_LeansWithThePosition_AndNeverBeyondItsReach()
    {
        var a = For(ControllerButton.LeftStickClick);
        var reach = a.Part.Travel;

        a.SetStick(1, 0);
        Run(a, 0.8);
        Assert.Equal(reach, a.Pose.Matrix.E, 1);
        Assert.Equal(0, a.Pose.Matrix.F, 1);

        a.SetStick(1, 1);   // a corner is further than the edge; the cap's reach is a circle
        Run(a, 0.8);
        Assert.Equal(reach * Math.Sqrt(0.5), a.Pose.Matrix.E, 1);
        Assert.Equal(reach * Math.Sqrt(0.5), a.Pose.Matrix.F, 1);

        a.SetStick(0, 0);
        Run(a, 1);
        Assert.Equal(0, a.Pose.Matrix.E, 1);
        Assert.True(a.IsSettled);
    }

    [Fact]
    public void AStick_ShrinksWhileItIsClicked()
    {
        var a = For(ControllerButton.RightStickClick);

        a.SetPressed(true);
        Run(a, 0.5);

        Assert.Equal(a.Part.PressScale, a.Pose.Matrix.A, 2);
        Assert.True(a.Pose.Matrix.A < 1);
    }

    [Fact]
    public void SelectionAndHover_FadeInAndOut()
    {
        var a = For(ControllerButton.Y);

        a.SetSelected(true);
        a.SetHover(true);
        Run(a, 0.8);
        Assert.Equal(1, a.Selection, 2);
        Assert.Equal(1, a.Hover, 2);

        a.SetSelected(false);
        a.SetHover(false);
        Run(a, 0.8);
        Assert.Equal(0, a.Selection, 2);
        Assert.True(a.IsSettled);
    }

    [Fact]
    public void ALongFrame_DoesNotMakeTheMotionBlowUp()
    {
        var a = For(ControllerButton.LeftStickClick);

        a.SetStick(1, -1);
        a.Step(5);   // the app was stalled for five seconds

        Assert.InRange(a.Pose.Matrix.E, -30, 30);
        Assert.InRange(a.Pose.Matrix.F, -30, 30);
    }

    [Fact]
    public void Snap_JumpsStraightToTheTargets()
    {
        var a = For(ControllerButton.X);

        a.SetPressed(true);
        a.SetSelected(true);
        a.Snap();

        Assert.Equal(1, a.Pose.Glow, 3);
        Assert.Equal(1, a.Selection, 3);
        Assert.True(a.IsSettled);
    }
}
