using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class CommandWheelTests
{
    private long _now;
    private MappingEngine _engine = null!;

    private static WheelItem Item(string label, string script)
    {
        Assert.True(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out var macro, out var error), error);
        return new WheelItem { Label = label, Macro = macro };
    }

    private Profile Use(int items = 4, ControllerButton button = ControllerButton.LeftBumper, bool rightStick = true)
    {
        var profile = Profile.CreateDefault();
        profile.RightStick.DeadzoneMin = 0.1;
        profile.LeftStick.DeadzoneMin = 0.1;
        profile.Wheel = new CommandWheel { Button = button, UseRightStick = rightStick };
        for (int i = 0; i < items; i++) profile.Wheel.Items.Add(Item("Item" + i, $"KeyTap:{(char)('A' + i)}"));
        _engine = new MappingEngine(profile, () => _now) { IsActive = true };
        return profile;
    }

    private RemappedState Step(long at, double rx, double ry, params ControllerButton[] pressed)
    {
        _now = at;
        return _engine.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0, rx, ry, 0, 0);
    }

    private const ControllerButton LB = ControllerButton.LeftBumper;

    // ---- which slice ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 4, 0)]       // up
    [InlineData(1, 0, 4, 1)]       // right
    [InlineData(0, -1, 4, 2)]      // down
    [InlineData(-1, 0, 4, 3)]      // left
    [InlineData(0.5, 0.9, 4, 0)]
    [InlineData(0.9, 0.5, 4, 1)]
    [InlineData(0, 1, 8, 0)]
    [InlineData(0.7, 0.7, 8, 1)]   // up-right is its own slice with eight
    [InlineData(1, 0, 8, 2)]
    [InlineData(-0.7, 0.7, 8, 7)]
    [InlineData(0, 1, 3, 0)]
    [InlineData(0.9, -0.5, 3, 1)]
    [InlineData(-0.9, -0.5, 3, 2)]
    public void TheStickPointsAtTheRightSlice(double x, double y, int count, int expected) =>
        Assert.Equal(expected, CommandWheel.SliceAt(x, y, count, 0.5));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.3, 0.3)]
    [InlineData(double.NaN, 1)]
    public void ASmallPush_ChoosesNothing(double x, double y) =>
        Assert.Equal(-1, CommandWheel.SliceAt(x, y, 4, 0.5));

    [Fact]
    public void FewerThanTwoSlices_IsNotAWheel()
    {
        Assert.Equal(-1, CommandWheel.SliceAt(0, 1, 1, 0.5));
        Assert.False(new CommandWheel { Button = LB, Items = { new WheelItem() } }.IsOn);
        Assert.False(new CommandWheel { Items = { new WheelItem(), new WheelItem() } }.IsOn);   // no button chosen
        Assert.True(new CommandWheel { Button = LB, Items = { new WheelItem(), new WheelItem() } }.IsOn);
    }

    [Fact]
    public void TheSliceAngles_TileTheCircle()
    {
        for (int count = 2; count <= 8; count++)
        {
            double covered = 0;
            for (int i = 0; i < count; i++)
            {
                var (start, end) = CommandWheel.SliceAngles(i, count);
                covered += end - start;
                if (i > 0) Assert.Equal(CommandWheel.SliceAngles(i - 1, count).End, start, 6);       // no gaps or overlaps
            }
            Assert.Equal(360, covered, 6);
        }
        Assert.Equal((-45, 45), CommandWheel.SliceAngles(0, 4));
    }

    // ---- in the engine ----------------------------------------------------------------------------------------------

    [Fact]
    public void HoldingTheButton_OpensTheWheel_AndTheStickChoosesASlice()
    {
        Use();

        Assert.False(Step(0, 0, 0).WheelOpen);

        var open = Step(10, 0, 0, LB);
        Assert.True(open.WheelOpen);
        Assert.Equal(-1, open.WheelIndex);

        Assert.Equal(1, Step(20, 1, 0, LB).WheelIndex);
        Assert.Equal(2, Step(30, 0, -1, LB).WheelIndex);
        Assert.Equal(-1, Step(40, 0.02, 0.02, LB).WheelIndex);
    }

    [Fact]
    public void WhileOpen_TheButtonAndTheStickDoNothingElse()
    {
        var profile = Use();
        profile.RightStick.Mode = StickMode.Mouse;

        var state = Step(10, 1, 0, LB);

        Assert.DoesNotContain(LB, state.ActiveVirtualButtons);
        Assert.Equal(0, state.RightStickX);
        Assert.Equal(0, state.MouseVelocityX);
    }

    [Fact]
    public void LettingGo_RunsTheChosenSlice_Once()
    {
        Use();
        Step(0, 0, 0, LB);
        Step(10, 1, 0, LB);                                     // pointing right: the second slice

        var release = Step(20, 0, 0);
        Assert.False(release.WheelOpen);
        var macro = Assert.Single(release.ActiveMacros);
        Assert.Equal("KeyTap:B", macro.ToScript());

        Assert.Single(Step(30, 0, 0).ActiveMacros);             // still inside its short pulse, same macro (the runner starts it once)
        Assert.Empty(Step(500, 0, 0).ActiveMacros);
    }

    [Fact]
    public void LettingGoWithNothingChosen_RunsNothing()
    {
        Use();
        Step(0, 0, 0, LB);
        Step(10, 0.05, 0, LB);

        Assert.Empty(Step(20, 0, 0).ActiveMacros);
        Assert.Empty(Step(100, 0, 0).ActiveMacros);
    }

    [Fact]
    public void AStickPushedAfterTheButtonIsLetGo_DoesNotRunAnything()
    {
        Use();
        Step(0, 0, 0, LB);
        Step(10, 0, 0, LB);

        Assert.Empty(Step(20, 1, 0).ActiveMacros);             // chosen only while the button was down
    }

    [Fact]
    public void TheLeftStickCanBeTheOne()
    {
        Use(rightStick: false);
        _now = 10;
        var state = _engine.ProcessInputs(new HashSet<ControllerButton> { LB }, 0, 1, 0.9, 0, 0, 0);   // left stick up, right stick pushed (ignored)

        Assert.Equal(0, state.WheelIndex);
        Assert.True(state.RightStickX > 0.8);                   // the right stick is not taken
        Assert.Equal(0, state.LeftStickY);
    }

    [Fact]
    public void WithoutAButton_OrWithOneSlice_NothingChanges()
    {
        var profile = Use(items: 4, button: ControllerButton.None);
        var state = Step(0, 1, 0, LB);
        Assert.False(state.WheelOpen);
        Assert.Contains(LB, state.ActiveVirtualButtons);

        profile.Wheel.Button = LB;
        profile.Wheel.Items.RemoveRange(1, 3);
        Assert.False(Step(10, 1, 0, LB).WheelOpen);
    }

    [Fact]
    public void LockingTheController_ClosesTheWheel()
    {
        Use();
        Assert.True(Step(0, 0, 0, LB).WheelOpen);

        _engine.SetLocked(true);

        Assert.False(Step(10, 0, 0, LB).WheelOpen);
    }

    [Fact]
    public void TheWheel_SurvivesSaving_AndOldProfilesHaveNone()
    {
        var profile = Use(3);
        profile.Wheel.Threshold = 0.7;
        profile.Wheel.UseRightStick = false;

        var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile)).Wheel;

        Assert.Equal(LB, back.Button);
        Assert.Equal(0.7, back.Threshold);
        Assert.False(back.UseRightStick);
        Assert.Equal(new[] { "Item0", "Item1", "Item2" }, back.Items.Select(i => i.Label));
        Assert.Equal("KeyTap:C", back.Items[2].Macro.ToScript());

        Assert.False(ProfileSerializer.FromJson("""{ "Name": "Old" }""").Wheel.IsOn);
    }
}
