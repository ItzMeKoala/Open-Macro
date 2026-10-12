using ReWASD.Core.Models;
using ReWASD.Core.Serialization;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class ComboTests
{
    private long _now;
    private MappingEngine _engine = null!;

    private static ComboMapping Chord(string key, params ControllerButton[] buttons) => new()
    {
        Kind = ComboKind.Chord,
        Buttons = buttons.ToList(),
        Binding = new ActivatorBinding { Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = key } }
    };

    private static ComboMapping Sequence(string key, params ControllerButton[] buttons) => new()
    {
        Kind = ComboKind.Sequence,
        Buttons = buttons.ToList(),
        Binding = new ActivatorBinding { Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = key } }
    };

    private void Use(params ComboMapping[] combos)
    {
        var profile = Profile.CreateDefault();
        profile.Combos = combos.ToList();
        profile.ChordWindowMs = 100;
        _engine = new MappingEngine(profile, () => _now) { IsActive = true };
    }

    private RemappedState Step(long at, params ControllerButton[] pressed)
    {
        _now = at;
        return _engine.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0, 0, 0, 0, 0);
    }

    private const ControllerButton A = ControllerButton.A, B = ControllerButton.B, X = ControllerButton.X;

    // ---- chords -------------------------------------------------------------------------------------------------

    [Fact]
    public void ButtonsPressedTogether_FireTheChord_AndNeitherActsAlone()
    {
        Use(Chord("Space", A, B));

        var first = Step(0, A);
        Assert.Empty(first.ActiveVirtualButtons);                      // A waits to see whether B follows

        var both = Step(40, A, B);
        Assert.Contains("Space", both.ActiveKeyboardKeys);
        Assert.DoesNotContain(A, both.ActiveVirtualButtons);
        Assert.DoesNotContain(B, both.ActiveVirtualButtons);

        var held = Step(500, A, B);
        Assert.Contains("Space", held.ActiveKeyboardKeys);             // held for as long as they are
        Assert.Empty(held.ActiveVirtualButtons);

        var gone = Step(600);
        Assert.Empty(gone.ActiveKeyboardKeys);
    }

    [Fact]
    public void AButtonPressedAlone_ActsOnceTheWindowHasPassed()
    {
        Use(Chord("Space", A, B));

        Assert.Empty(Step(0, A).ActiveVirtualButtons);
        Assert.Empty(Step(99, A).ActiveVirtualButtons);
        Assert.Contains(A, Step(100, A).ActiveVirtualButtons);
        Assert.Contains(A, Step(300, A).ActiveVirtualButtons);
    }

    [Fact]
    public void ButtonsNotInAChord_ActAtOnce()
    {
        Use(Chord("Space", A, B));

        Assert.Contains(X, Step(0, X).ActiveVirtualButtons);
    }

    [Fact]
    public void ButtonsTooFarApart_AreNotAChord()
    {
        Use(Chord("Space", A, B));

        Step(0, A);
        Assert.Contains(A, Step(150, A).ActiveVirtualButtons);          // A has been acting alone since the window passed
        var late = Step(200, A, B);
        Assert.Empty(late.ActiveKeyboardKeys);
        Assert.Contains(A, late.ActiveVirtualButtons);
        Assert.Contains(B, Step(400, A, B).ActiveVirtualButtons);

        Step(500);                                                      // let go of both, then try again properly
        Step(600, A);
        Assert.Contains("Space", Step(650, A, B).ActiveKeyboardKeys);
    }

    [Fact]
    public void LettingGoOfOneButton_EndsTheChord_AndTheOtherStaysSilentUntilReleased()
    {
        Use(Chord("Space", A, B));
        Step(0, A, B);
        Assert.Contains("Space", Step(50, A, B).ActiveKeyboardKeys);

        var afterRelease = Step(300, A);
        Assert.Empty(afterRelease.ActiveKeyboardKeys);
        Assert.Empty(afterRelease.ActiveVirtualButtons);                // A is still held, but not passed on by surprise

        Step(400);
        Assert.Contains(A, Step(1000, A).ActiveVirtualButtons.Concat(Step(1200, A).ActiveVirtualButtons));   // a fresh press acts normally again
    }

    [Fact]
    public void ThreeButtonChords_NeedAllThree()
    {
        Use(Chord("F5", A, B, X));

        Assert.Empty(Step(0, A, B).ActiveKeyboardKeys);
        Assert.Contains("F5", Step(50, A, B, X).ActiveKeyboardKeys);
    }

    [Fact]
    public void ChordsSharingAButton_BothWork_EachWhenItsOwnButtonsAreHeld()
    {
        Use(Chord("F1", A, B), Chord("F2", A, X));

        Step(0, A, B);
        Assert.Contains("F1", Step(30, A, B).ActiveKeyboardKeys);
        Step(100);
        Step(200, A, X);
        var state = Step(230, A, X);
        Assert.Contains("F2", state.ActiveKeyboardKeys);
        Assert.DoesNotContain("F1", state.ActiveKeyboardKeys);
    }

    [Fact]
    public void AChordCanRunAMacroOrToggleOrPulse()
    {
        var pulse = Chord("Q", A, B);
        pulse.Binding.Mode = MappingMode.OneTime;
        Use(pulse);

        Step(0, A, B);
        Assert.Contains("Q", Step(10, A, B).ActiveKeyboardKeys);
        Assert.Empty(Step(200, A, B).ActiveKeyboardKeys);               // a pulse, not held
    }

    // ---- sequences ----------------------------------------------------------------------------------------------

    private static readonly ControllerButton Up = ControllerButton.DpadUp, Down = ControllerButton.DpadDown;

    [Fact]
    public void ASequence_FiresOnTheLastPress_AndOnlyInOrder()
    {
        Use(Sequence("K", Up, Up, Down));

        Step(0, Up); Step(50);
        Step(100, Up); Step(150);
        var fired = Step(200, Down);
        Assert.Contains("K", fired.ActiveKeyboardKeys);

        Assert.Contains("K", Step(210, Down).ActiveKeyboardKeys);       // a short pulse
        Assert.Empty(Step(400).ActiveKeyboardKeys);
    }

    [Fact]
    public void ASequence_BreaksOnAWrongButtonOrALongPause()
    {
        Use(Sequence("K", Up, Up, Down));

        Step(0, Up); Step(50);
        Step(100, X); Step(150);                                          // wrong button
        Step(200, Up); Step(250);
        Assert.Empty(Step(300, Down).ActiveKeyboardKeys);                 // only one Up since

        Step(350); Step(400, Up); Step(450);
        Step(500, Up); Step(550);
        Assert.Empty(Step(2000, Down).ActiveKeyboardKeys);                // too long a pause
    }

    [Fact]
    public void ASequence_CanBeRepeated_AndRestartsOnItsFirstButton()
    {
        Use(Sequence("K", Up, Down));

        Step(0, Up); Step(10);
        Assert.Contains("K", Step(20, Down).ActiveKeyboardKeys);
        Step(100);

        Step(200, Up); Step(210); Step(220, Up); Step(230);               // Up twice: the second Up starts it over
        Assert.Contains("K", Step(240, Down).ActiveKeyboardKeys);
    }

    [Fact]
    public void ASequence_DoesNotStopTheButtonsFromActingNormally()
    {
        Use(Sequence("K", Up, Down));

        Assert.Contains(Up, Step(0, Up).ActiveVirtualButtons);
    }

    // ---- housekeeping ---------------------------------------------------------------------------------------------

    [Fact]
    public void WithoutCombos_NothingIsDelayed()
    {
        Use();
        Assert.Contains(A, Step(0, A).ActiveVirtualButtons);
    }

    [Fact]
    public void RemovingACombo_ForgetsItImmediately()
    {
        var chord = Chord("Space", A, B);
        Use(chord);
        Step(0, A, B);
        Assert.Contains("Space", Step(20, A, B).ActiveKeyboardKeys);

        _engine.CurrentProfile.Combos.Clear();

        var state = Step(40, A, B);
        Assert.Empty(state.ActiveKeyboardKeys);
        Assert.Contains(A, state.ActiveVirtualButtons);
    }

    [Fact]
    public void LockingTheController_StopsCombosToo()
    {
        Use(Chord("Space", A, B));
        Step(0, A, B);
        Assert.Contains("Space", Step(20, A, B).ActiveKeyboardKeys);

        _engine.SetLocked(true);
        Assert.Empty(Step(40, A, B).ActiveKeyboardKeys);
    }

    [Fact]
    public void Names_AreReadFromTypedText()
    {
        Assert.True(ComboMapping.Names.TryParse("A+B", out var ab, out _));
        Assert.Equal(new[] { A, B }, ab);

        Assert.True(ComboMapping.Names.TryParse("Up, up Down;LB rb L3 start", out var many, out _));
        Assert.Equal(new[]
        {
            ControllerButton.DpadUp, ControllerButton.DpadUp, ControllerButton.DpadDown, ControllerButton.LeftBumper,
            ControllerButton.RightBumper, ControllerButton.LeftStickClick, ControllerButton.Menu
        }, many);

        Assert.False(ComboMapping.Names.TryParse("A+Banana", out _, out var error));
        Assert.Contains("Banana", error);
        Assert.False(ComboMapping.Names.TryParse("None", out _, out _));
        Assert.Equal("Up, Up, Down", new ComboMapping { Kind = ComboKind.Sequence, Buttons = { ControllerButton.DpadUp, ControllerButton.DpadUp, ControllerButton.DpadDown } }.Description);
        Assert.Equal("LB + A", new ComboMapping { Buttons = { ControllerButton.LeftBumper, A } }.Description);
    }

    [Fact]
    public void Combos_SurviveSaving_AndOldProfilesStillLoad()
    {
        var profile = Profile.CreateDefault();
        profile.ChordWindowMs = 80;
        var chord = Chord("Space", A, B);
        chord.Binding.Mode = MappingMode.Toggle;
        profile.Combos.Add(chord);
        profile.Combos.Add(Sequence("K", Up, Down));

        var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));

        Assert.Equal(80, loaded.ChordWindowMs);
        Assert.Equal(2, loaded.Combos.Count);
        Assert.Equal(chord.Id, loaded.Combos[0].Id);
        Assert.Equal(ComboKind.Chord, loaded.Combos[0].Kind);
        Assert.Equal(new[] { A, B }, loaded.Combos[0].Buttons);
        Assert.Equal(MappingMode.Toggle, loaded.Combos[0].Binding.Mode);
        Assert.Equal("Space", loaded.Combos[0].Binding.Target.KeyboardKey);
        Assert.Equal(ComboKind.Sequence, loaded.Combos[1].Kind);

        Assert.Empty(ProfileSerializer.FromJson("""{ "Name": "Old" }""").Combos);
    }
}
