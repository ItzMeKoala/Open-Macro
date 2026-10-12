using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class StickModeTests
{
    private sealed class FakeBackend : IInputBackend
    {
        public List<string> Events { get; } = new();
        public void KeyDown(ushort virtualKey) => Events.Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Events.Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Events.Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Events.Add($"mu:{button}");
        public void MouseWheel(int delta) => Events.Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Events.Add($"move:{dx},{dy}");
    }

    private static RemappedState Run(Profile profile, double lx, double ly, double rx = 0, double ry = 0, params ControllerButton[] pressed)
    {
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };
        return engine.ProcessInputs(new HashSet<ControllerButton>(pressed), lx, ly, rx, ry, 0, 0);
    }

    private static Profile WithLeft(StickMode mode)
    {
        var profile = Profile.CreateDefault();
        profile.LeftStick.Mode = mode;
        profile.LeftStick.DeadzoneMin = 0.1;
        return profile;
    }

    [Fact]
    public void Gamepad_IsTheDefault_AndKeepsDrivingTheVirtualStick()
    {
        var state = Run(Profile.CreateDefault(), 1, 0);

        Assert.Equal(1.0, state.LeftStickX);
        Assert.Equal(0, state.MouseVelocityX);
        Assert.Empty(state.ActiveKeyboardKeys);
    }

    [Fact]
    public void MouseMode_MovesTheCursor_AndTakesTheStickAwayFromTheVirtualPad()
    {
        var profile = WithLeft(StickMode.Mouse);
        profile.LeftStick.MouseSpeed = 1000;

        var right = Run(profile, 1, 0);
        Assert.Equal(1000, right.MouseVelocityX, 3);
        Assert.Equal(0, right.MouseVelocityY, 3);
        Assert.Equal(0, right.LeftStickX);

        // Stick up moves the cursor up the screen (negative y)
        var up = Run(profile, 0, 1);
        Assert.Equal(-1000, up.MouseVelocityY, 3);
    }

    [Fact]
    public void MouseMode_ScalesWithDeflection_AndIgnoresTheDeadzone()
    {
        var profile = WithLeft(StickMode.Mouse);
        profile.LeftStick.MouseSpeed = 1000;

        Assert.Equal(0, Run(profile, 0.05, 0).MouseVelocityX);
        var half = Run(profile, 0.55, 0).MouseVelocityX;
        Assert.InRange(half, 400, 600);
    }

    [Fact]
    public void MouseMode_InvertAndPrecisionButton_ApplyToTheCursor()
    {
        var profile = WithLeft(StickMode.Mouse);
        profile.LeftStick.MouseSpeed = 1000;
        profile.LeftStick.InvertX = true;
        profile.LeftStick.PrecisionButton = ControllerButton.RightTrigger;
        profile.LeftStick.PrecisionFactor = 0.25;

        Assert.Equal(-1000, Run(profile, 1, 0).MouseVelocityX, 3);
        Assert.Equal(-250, Run(profile, 1, 0, 0, 0, ControllerButton.RightTrigger).MouseVelocityX, 3);
    }

    [Fact]
    public void BothSticksInMouseMode_AddUp()
    {
        var profile = WithLeft(StickMode.Mouse);
        profile.RightStick.Mode = StickMode.Mouse;
        profile.RightStick.DeadzoneMin = 0.1;
        profile.LeftStick.MouseSpeed = 1000;
        profile.RightStick.MouseSpeed = 200;

        Assert.Equal(1200, Run(profile, 1, 0, 1, 0).MouseVelocityX, 3);
    }

    [Fact]
    public void ScrollMode_ScrollsWithTheVerticalAxis()
    {
        var profile = WithLeft(StickMode.Scroll);
        profile.LeftStick.ScrollSpeed = 10;

        Assert.Equal(10, Run(profile, 0, 1).ScrollVelocity, 3);
        Assert.Equal(-10, Run(profile, 0, -1).ScrollVelocity, 3);
        Assert.Equal(0, Run(profile, 1, 0).ScrollVelocity, 3);
    }

    [Fact]
    public void KeysMode_HoldsAKeyPerDirection_WithDiagonals()
    {
        var profile = WithLeft(StickMode.Keys);

        Assert.Equal(new[] { "W" }, Run(profile, 0, 1).ActiveKeyboardKeys.OrderBy(k => k));
        Assert.Equal(new[] { "A", "S" }, Run(profile, -1, -1).ActiveKeyboardKeys.OrderBy(k => k));
        Assert.Equal(new[] { "D", "W" }, Run(profile, 0.8, 0.8).ActiveKeyboardKeys.OrderBy(k => k));
        Assert.Empty(Run(profile, 0, 0).ActiveKeyboardKeys);
        Assert.Empty(Run(profile, 0.3, 0.3).ActiveKeyboardKeys);   // not far enough
        Assert.Equal(0, Run(profile, 0, 1).LeftStickY);            // the virtual stick is left alone
    }

    [Fact]
    public void KeysMode_UsesTheChosenKeysAndThreshold()
    {
        var profile = WithLeft(StickMode.Keys);
        profile.LeftStick.UseKeyPreset(arrows: true);
        profile.LeftStick.KeyThreshold = 0.8;

        Assert.Equal(new[] { "Up" }, Run(profile, 0, 1).ActiveKeyboardKeys);
        Assert.Empty(Run(profile, 0, 0.6).ActiveKeyboardKeys);

        profile.LeftStick.KeyUp = "Space";
        Assert.Equal(new[] { "Space" }, Run(profile, 0, 1).ActiveKeyboardKeys);
    }

    [Fact]
    public void ALayerCanOverrideAStickMode_AndLeavesTheOtherStickAlone()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[1].ModifierButtons.Add(ControllerButton.LeftBumper);
        profile.ShiftLayers[1].LeftStickMode = StickMode.Keys;
        profile.LeftStick.DeadzoneMin = 0.1;

        var main = Run(profile, 0, 1, 1, 0);
        Assert.Equal(1.0, main.LeftStickY);
        Assert.Empty(main.ActiveKeyboardKeys);

        var layered = Run(profile, 0, 1, 1, 0, ControllerButton.LeftBumper);
        Assert.Equal(new[] { "W" }, layered.ActiveKeyboardKeys);
        Assert.Equal(0, layered.LeftStickY);
        Assert.Equal(1.0, layered.RightStickX);
    }

    [Fact]
    public void AStickWhoseDirectionsWereRemapped_IgnoresItsMode()
    {
        var profile = WithLeft(StickMode.Mouse);
        profile.ShiftLayers[0].Mappings[ControllerButton.LeftStickUp].Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "Space" };

        Assert.Equal(0, Run(profile, 1, 0).MouseVelocityX);
    }

    [Fact]
    public void WithRemappingOff_TheStickPassesStraightThrough()
    {
        var profile = WithLeft(StickMode.Mouse);
        var engine = new MappingEngine(profile, () => 0) { IsActive = false };

        var state = engine.ProcessInputs(new HashSet<ControllerButton>(), 1, 0, 0, 0, 0, 0);

        Assert.Equal(1.0, state.LeftStickX);
        Assert.Equal(0, state.MouseVelocityX);
    }

    [Fact]
    public void Settings_SurviveSaving_AndOldProfilesStillLoad()
    {
        var profile = Profile.CreateDefault();
        profile.LeftStick.Mode = StickMode.Keys;
        profile.LeftStick.UseKeyPreset(arrows: true);
        profile.LeftStick.PrecisionButton = ControllerButton.LeftTrigger;
        profile.RightStick.Mode = StickMode.Scroll;
        profile.ShiftLayers[2].RightStickMode = StickMode.Mouse;

        var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));

        Assert.Equal(StickMode.Keys, loaded.LeftStick.Mode);
        Assert.Equal("Up", loaded.LeftStick.KeyUp);
        Assert.Equal(ControllerButton.LeftTrigger, loaded.LeftStick.PrecisionButton);
        Assert.Equal(StickMode.Scroll, loaded.RightStick.Mode);
        Assert.Equal(StickMode.Mouse, loaded.ShiftLayers[2].RightStickMode);
        Assert.Null(loaded.ShiftLayers[1].LeftStickMode);

        var old = ProfileSerializer.FromJson("""{ "Name": "Old", "LeftStick": { "Sensitivity": 0.5 } }""");
        Assert.Equal(StickMode.Gamepad, old.LeftStick.Mode);
        Assert.Equal(0.5, old.LeftStick.Sensitivity);
    }

    // ---- turning speeds into movement ----------------------------------------------------------------------------

    [Fact]
    public void TheInjector_MovesBySpeedTimesTime_AndCarriesTheFractions()
    {
        long now = 0;
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend, () => now);
        var state = new RemappedState { MouseVelocityX = 150, MouseVelocityY = -100 };

        injector.Apply(state);                // first frame only starts the clock
        for (int i = 0; i < 10; i++) { now += 10; injector.Apply(state); }

        // 100 ms at 150 px/s = 15 px right; at 100 px/s = 10 px up, spread across frames without losing any
        var moves = backend.Events.Where(e => e.StartsWith("move:")).Select(e => e[5..].Split(',').Select(int.Parse).ToArray()).ToList();
        Assert.Equal(15, moves.Sum(m => m[0]));
        Assert.Equal(-10, moves.Sum(m => m[1]));
        Assert.All(moves, m => Assert.True(Math.Abs(m[0]) <= 2));
    }

    [Fact]
    public void TheInjector_DoesNothingWithoutSpeed_AndForgetsLeftoverWhenTheStickCentres()
    {
        long now = 0;
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend, () => now);

        injector.Apply(new RemappedState());
        now += 10; injector.Apply(new RemappedState { MouseVelocityX = 40 });   // 0.4 px: not a whole pixel yet
        now += 10; injector.Apply(new RemappedState());                         // centred: the 0.4 is dropped
        now += 10; injector.Apply(new RemappedState { MouseVelocityX = 40 });   // another 0.4, still no move

        Assert.Empty(backend.Events);
    }

    [Fact]
    public void TheInjector_NeverJumpsAfterALongPause()
    {
        long now = 0;
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend, () => now);
        var state = new RemappedState { MouseVelocityX = 1000 };

        injector.Apply(state);
        now += 5000;                          // the app stalled for five seconds
        injector.Apply(state);

        Assert.Equal(new[] { "move:100,0" }, backend.Events);   // at most 0.1 s of movement
    }

    [Fact]
    public void TheInjector_ScrollsInWholeNotches()
    {
        long now = 0;
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend, () => now);
        var state = new RemappedState { ScrollVelocity = -10 };

        injector.Apply(state);
        for (int i = 0; i < 10; i++) { now += 50; injector.Apply(state); }   // half a second at 10 notches/s

        Assert.Equal(5, backend.Events.Count(e => e == "wheel:-120"));
    }

    [Fact]
    public void ReleaseAll_StopsTheCursor()
    {
        long now = 0;
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend, () => now);
        injector.Apply(new RemappedState { MouseVelocityX = 30 });
        now += 10; injector.Apply(new RemappedState { MouseVelocityX = 30 });
        injector.ReleaseAll();
        now += 10; injector.Apply(new RemappedState { MouseVelocityX = 30 });   // restarts its clock, so no catch-up

        Assert.Empty(backend.Events);
    }
}
