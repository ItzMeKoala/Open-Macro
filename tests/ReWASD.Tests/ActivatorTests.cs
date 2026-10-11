using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class ActivatorTests
{
    private static RemapTarget Key(string name) => new() { Type = TargetType.KeyboardKey, KeyboardKey = name };

    private static ActivatorBinding Bind(ActivatorType type, string key, MappingMode mode = MappingMode.Normal) =>
        new() { Type = type, Target = Key(key), Mode = mode };

    /// <summary>An engine on a controllable clock where button A has the given bindings.</summary>
    private sealed class Rig
    {
        public long Now;
        public Profile Profile = Profile.CreateDefault();
        public MappingEngine Engine = null!;

        public Rig(MappingMode singleMode = MappingMode.Normal, params ActivatorBinding[] extra)
        {
            var mapping = Profile.ShiftLayers[0].Mappings[ControllerButton.A];
            mapping.Target = Key("S");
            mapping.Mode = singleMode;
            foreach (var b in extra) mapping.SetBinding(b.Type, b);
            Engine = new MappingEngine(Profile, () => Now) { IsActive = true };
        }

        public RemappedState Frame(long at, params ControllerButton[] pressed)
        {
            Now = at;
            return Engine.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0, 0, 0, 0, 0);
        }
    }

    private const ControllerButton A = ControllerButton.A;

    [Fact]
    public void SingleOnly_HoldsWhileDown_AndFiresImmediately()
    {
        var rig = new Rig();

        Assert.Contains("S", rig.Frame(0, A).ActiveKeyboardKeys);
        Assert.Contains("S", rig.Frame(500, A).ActiveKeyboardKeys);
        Assert.Empty(rig.Frame(501).ActiveKeyboardKeys);
    }

    [Fact]
    public void LongAndSingle_QuickTapFiresSingleAfterRelease_NotLong()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Long, "L"));

        Assert.Empty(rig.Frame(0, A).ActiveKeyboardKeys);           // single must wait to rule out a long press
        Assert.Empty(rig.Frame(100, A).ActiveKeyboardKeys);

        var released = rig.Frame(120);                                // released before the long threshold
        Assert.Contains("S", released.ActiveKeyboardKeys);
        Assert.DoesNotContain("L", released.ActiveKeyboardKeys);

        Assert.Empty(rig.Frame(220).ActiveKeyboardKeys);              // it was only a pulse
    }

    [Fact]
    public void LongAndSingle_HoldPastThresholdFiresLongOnly_UntilRelease()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Long, "L"));

        Assert.Empty(rig.Frame(0, A).ActiveKeyboardKeys);
        Assert.Empty(rig.Frame(399, A).ActiveKeyboardKeys);

        var fired = rig.Frame(400, A);
        Assert.Contains("L", fired.ActiveKeyboardKeys);
        Assert.DoesNotContain("S", fired.ActiveKeyboardKeys);
        Assert.Contains("L", rig.Frame(900, A).ActiveKeyboardKeys);

        var afterRelease = rig.Frame(901);
        Assert.Empty(afterRelease.ActiveKeyboardKeys);                // no stray Single after a long press
        Assert.Empty(rig.Frame(1300).ActiveKeyboardKeys);
    }

    [Fact]
    public void DoubleAndSingle_DoublePressFiresDouble_NotSingle()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Double, "D"));

        Assert.Empty(rig.Frame(0, A).ActiveKeyboardKeys);
        Assert.Empty(rig.Frame(50).ActiveKeyboardKeys);

        var second = rig.Frame(100, A);                               // second press inside the window
        Assert.Contains("D", second.ActiveKeyboardKeys);
        Assert.DoesNotContain("S", second.ActiveKeyboardKeys);

        Assert.Contains("D", rig.Frame(400, A).ActiveKeyboardKeys);   // the final tap can be held
        Assert.Empty(rig.Frame(401).ActiveKeyboardKeys);
        Assert.Empty(rig.Frame(800).ActiveKeyboardKeys);              // single never fired
    }

    [Fact]
    public void DoubleAndSingle_LoneTapFiresSingleOnlyAfterWindow()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Double, "D"));

        rig.Frame(0, A);
        Assert.Empty(rig.Frame(50).ActiveKeyboardKeys);               // released, still waiting for a second tap
        Assert.Empty(rig.Frame(200).ActiveKeyboardKeys);

        Assert.Contains("S", rig.Frame(300).ActiveKeyboardKeys);      // 50 + 250ms window elapsed
        Assert.Empty(rig.Frame(400).ActiveKeyboardKeys);
    }

    [Fact]
    public void TriplePress_FiresTriple()
    {
        var rig = new Rig(extra: new[] { Bind(ActivatorType.Double, "D"), Bind(ActivatorType.Triple, "T") });

        rig.Frame(0, A); rig.Frame(30);
        var second = rig.Frame(60, A);
        Assert.Empty(second.ActiveKeyboardKeys);                      // a triple is still possible, so double must wait
        rig.Frame(90);

        var third = rig.Frame(120, A);
        Assert.Contains("T", third.ActiveKeyboardKeys);
        Assert.DoesNotContain("D", third.ActiveKeyboardKeys);
    }

    [Fact]
    public void DoubleAndTriple_TwoTapsThenPause_FiresDoubleAfterWindow()
    {
        var rig = new Rig(extra: new[] { Bind(ActivatorType.Double, "D"), Bind(ActivatorType.Triple, "T") });

        rig.Frame(0, A); rig.Frame(30); rig.Frame(60, A); rig.Frame(90);

        Assert.Empty(rig.Frame(200).ActiveKeyboardKeys);
        Assert.Contains("D", rig.Frame(340).ActiveKeyboardKeys);
    }

    [Fact]
    public void StartActivator_FiresOnPress_EvenWhenSingleIsDeferred()
    {
        var rig = new Rig(extra: new[] { Bind(ActivatorType.Start, "G"), Bind(ActivatorType.Long, "L") });

        var press = rig.Frame(0, A);
        Assert.Contains("G", press.ActiveKeyboardKeys);
        Assert.DoesNotContain("S", press.ActiveKeyboardKeys);
    }

    [Fact]
    public void ReleaseActivator_FiresAsPulseOnRelease()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Release, "R"));

        Assert.DoesNotContain("R", rig.Frame(0, A).ActiveKeyboardKeys);
        Assert.Contains("R", rig.Frame(200).ActiveKeyboardKeys);
        Assert.DoesNotContain("R", rig.Frame(300).ActiveKeyboardKeys);
    }

    [Fact]
    public void OneTimeMode_PulsesOnceEvenWhileStillHeld()
    {
        var rig = new Rig(MappingMode.OneTime);

        Assert.Contains("S", rig.Frame(0, A).ActiveKeyboardKeys);
        Assert.Contains("S", rig.Frame(20, A).ActiveKeyboardKeys);
        Assert.DoesNotContain("S", rig.Frame(100, A).ActiveKeyboardKeys);
        Assert.DoesNotContain("S", rig.Frame(2000, A).ActiveKeyboardKeys);
    }

    [Fact]
    public void ToggleOnDoublePress_LatchesUntilNextDoublePress()
    {
        var rig = new Rig(extra: Bind(ActivatorType.Double, "D", MappingMode.Toggle));

        rig.Frame(0, A); rig.Frame(30);
        Assert.Contains("D", rig.Frame(60, A).ActiveKeyboardKeys);
        Assert.Contains("D", rig.Frame(90).ActiveKeyboardKeys);       // stays on after release
        Assert.Contains("D", rig.Frame(1000).ActiveKeyboardKeys);

        rig.Frame(1100, A); rig.Frame(1130);
        Assert.DoesNotContain("D", rig.Frame(1160, A).ActiveKeyboardKeys);
    }

    [Fact]
    public void RepeatOnLong_AlternatesWhileHeld()
    {
        var binding = Bind(ActivatorType.Long, "L", MappingMode.Turbo);
        binding.TurboRateMs = 50;
        var rig = new Rig(extra: binding);

        rig.Frame(0, A);
        Assert.Contains("L", rig.Frame(400, A).ActiveKeyboardKeys);
        Assert.DoesNotContain("L", rig.Frame(460, A).ActiveKeyboardKeys);
        Assert.Contains("L", rig.Frame(510, A).ActiveKeyboardKeys);
    }

    [Fact]
    public void JumpToLayer_SwitchesLayerWhileHeld()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].Mappings[ControllerButton.X].Target = new RemapTarget { Type = TargetType.JumpToLayer, Layer = 1 };
        profile.ShiftLayers[1].Mappings[ControllerButton.A] = new ButtonMapping { SourceButton = A, Target = Key("J") };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var jump = new HashSet<ControllerButton> { ControllerButton.X };
        engine.ProcessInputs(jump, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, engine.ActiveShiftLayerIndex);                // the jump takes effect on the next frame

        engine.ProcessInputs(jump, 0, 0, 0, 0, 0, 0);
        Assert.Equal(1, engine.ActiveShiftLayerIndex);

        var withA = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.X, A }, 0, 0, 0, 0, 0, 0);
        Assert.Contains("J", withA.ActiveKeyboardKeys);

        // Letting go of the jump button returns to Main for buttons pressed afterwards
        engine.ProcessInputs(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);
        engine.ProcessInputs(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, engine.ActiveShiftLayerIndex);
        var back = engine.ProcessInputs(new HashSet<ControllerButton> { A }, 0, 0, 0, 0, 0, 0);
        Assert.DoesNotContain("J", back.ActiveKeyboardKeys);
    }

    [Fact]
    public void ShiftChord_NeedsEveryButton_AndConsumesThem()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[1].ModifierButtons = new List<ControllerButton> { ControllerButton.LeftBumper, ControllerButton.RightBumper };
        profile.ShiftLayers[1].Mappings[A] = new ButtonMapping { SourceButton = A, Target = Key("J") };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var onlyOne = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftBumper }, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, engine.ActiveShiftLayerIndex);                // half a chord does not switch layers
        Assert.Contains(ControllerButton.LeftBumper, onlyOne.ActiveVirtualButtons);
        engine.ProcessInputs(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);

        engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftBumper, ControllerButton.RightBumper }, 0, 0, 0, 0, 0, 0);
        var chord = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftBumper, ControllerButton.RightBumper, A }, 0, 0, 0, 0, 0, 0);
        Assert.Equal(1, engine.ActiveShiftLayerIndex);
        Assert.Contains("J", chord.ActiveKeyboardKeys);
        Assert.DoesNotContain(ControllerButton.LeftBumper, chord.ActiveVirtualButtons);
        Assert.DoesNotContain(ControllerButton.RightBumper, chord.ActiveVirtualButtons);
    }

    [Fact]
    public void ToggleShift_StaysOnAfterRelease_AndTurnsOffOnNextPress()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[2].ModifierButtons = new List<ControllerButton> { ControllerButton.View };
        profile.ShiftLayers[2].Type = ShiftType.Toggle;
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.View }, 0, 0, 0, 0, 0, 0);
        Assert.Equal(2, engine.ActiveShiftLayerIndex);
        engine.ProcessInputs(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);
        Assert.Equal(2, engine.ActiveShiftLayerIndex);

        engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.View }, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, engine.ActiveShiftLayerIndex);
    }

    [Fact]
    public void ShiftLayer_InheritsMainMappings_UnlessOverridden()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[1].ModifierButtons = new List<ControllerButton> { ControllerButton.View };
        profile.ShiftLayers[0].Mappings[A] = new ButtonMapping { SourceButton = A, Target = Key("M") };
        profile.ShiftLayers[0].Mappings[ControllerButton.B] = new ButtonMapping { SourceButton = ControllerButton.B, Target = Key("N") };
        profile.ShiftLayers[1].Mappings[ControllerButton.B] = new ButtonMapping { SourceButton = ControllerButton.B, Target = Key("O") };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.View, A, ControllerButton.B }, 0, 0, 0, 0, 0, 0);

        Assert.Equal(1, engine.ActiveShiftLayerIndex);
        Assert.Contains("M", state.ActiveKeyboardKeys);               // inherited from Main
        Assert.Contains("O", state.ActiveKeyboardKeys);               // overridden in the shift
        Assert.DoesNotContain("N", state.ActiveKeyboardKeys);
    }

    [Fact]
    public void KeyboardSource_MapsToPadButton_AndIsSuppressed()
    {
        var profile = Profile.CreateDefault();
        profile.ShiftLayers[0].KeyboardMappings["F"] = new ButtonMapping { Target = RemapTarget.Default(ControllerButton.X) };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton>(), new[] { "f" }, Array.Empty<string>(), 0, 0, 0, 0, 0, 0);

        Assert.Contains(ControllerButton.X, state.ActiveVirtualButtons);
        Assert.True(engine.IsKeySuppressed("F"));
        Assert.False(engine.IsKeySuppressed("G"));

        engine.IsActive = false;
        Assert.False(engine.IsKeySuppressed("F"));
    }

    [Fact]
    public void UnmappedKey_IsNotTouched()
    {
        var engine = new MappingEngine(Profile.CreateDefault(), () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton>(), new[] { "q" }, Array.Empty<string>(), 0, 0, 0, 0, 0, 0);

        Assert.Empty(state.ActiveKeyboardKeys);
        Assert.Empty(state.ActiveVirtualButtons);
    }

    [Fact]
    public void MouseSource_WheelUpMapsToMacroTrigger()
    {
        var profile = Profile.CreateDefault();
        MacroDefinition.TryParseScript("KeyTap:E", MacroRepeatMode.Once, out var macro, out _);
        profile.ShiftLayers[0].MouseMappings["Wheel Up"] = new ButtonMapping { Target = new RemapTarget { Type = TargetType.Macro, Macro = macro } };
        var engine = new MappingEngine(profile, () => 0) { IsActive = true };

        var state = engine.ProcessInputs(new HashSet<ControllerButton>(), Array.Empty<string>(), new[] { "Wheel Up" }, 0, 0, 0, 0, 0, 0);

        Assert.Single(state.ActiveMacros);
        Assert.True(engine.IsMouseSuppressed("wheel up"));
    }

    [Fact]
    public void ProfileWithActivatorsAndKeyboardMappings_RoundTripsThroughJson()
    {
        var profile = Profile.CreateDefault();
        var mapping = profile.ShiftLayers[0].Mappings[A];
        mapping.SetBinding(ActivatorType.Long, Bind(ActivatorType.Long, "L", MappingMode.Turbo));
        profile.ShiftLayers[0].KeyboardMappings["W"] = new ButtonMapping { Target = RemapTarget.Default(ControllerButton.DpadUp) };
        profile.ShiftLayers[1].ModifierButtons = new List<ControllerButton> { ControllerButton.View, ControllerButton.Menu };
        profile.ShiftLayers[1].Type = ShiftType.Toggle;
        profile.Timings.LongPressMs = 650;

        var restored = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));

        var longBinding = restored.ShiftLayers[0].Mappings[A].GetBinding(ActivatorType.Long);
        Assert.NotNull(longBinding);
        Assert.Equal(MappingMode.Turbo, longBinding!.Mode);
        Assert.Equal("L", longBinding.Target.KeyboardKey);
        Assert.True(restored.ShiftLayers[0].KeyboardMappings.ContainsKey("w"));   // lookups stay case-insensitive after loading
        Assert.Equal(2, restored.ShiftLayers[1].ModifierButtons.Count);
        Assert.Equal(ShiftType.Toggle, restored.ShiftLayers[1].Type);
        Assert.Equal(650, restored.Timings.LongPressMs);
    }

    [Fact]
    public void ButtonMapping_Clone_CopiesActivators()
    {
        var mapping = new ButtonMapping { Target = Key("S") };
        mapping.SetBinding(ActivatorType.Double, Bind(ActivatorType.Double, "D"));

        var clone = mapping.Clone();
        clone.GetBinding(ActivatorType.Double)!.Target.KeyboardKey = "X";

        Assert.Equal("D", mapping.GetBinding(ActivatorType.Double)!.Target.KeyboardKey);
    }
}
