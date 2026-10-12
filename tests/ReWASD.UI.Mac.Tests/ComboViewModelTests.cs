using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.UI;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class ComboViewModelTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void AddingAChord_PutsItInTheProfile_RunningTheAction()
    {
        var vm = New();
        vm.NewComboKindIndex = 0;
        vm.NewComboButtons = "A + B";
        vm.NewComboAction = "KeyTap:Space";

        vm.AddComboCommand.Execute(null);

        var combo = Assert.Single(vm.SelectedProfile.Combos);
        Assert.Equal(ComboKind.Chord, combo.Kind);
        Assert.Equal(new[] { ControllerButton.A, ControllerButton.B }, combo.Buttons);
        Assert.Equal(TargetType.Macro, combo.Binding.Target.Type);
        Assert.Equal("KeyTap:Space", combo.Binding.Target.Macro!.ToScript());
        Assert.Contains(combo, vm.Combos);
        Assert.Equal("", vm.NewComboButtons);
        Assert.StartsWith("Added", vm.ComboMessage);
    }

    [Fact]
    public void AnAddedCombo_ReallyFiresInTheEngine()
    {
        var vm = New();
        vm.NewComboButtons = "LB RB";
        vm.NewComboAction = "KeyDown:W";
        vm.NewComboRepeatIndex = 2;                         // hold until released
        vm.AddComboCommand.Execute(null);

        long now = 0;
        var engine = new ReWASD.Engine.Remapper.MappingEngine(vm.SelectedProfile, () => now) { IsActive = true };
        engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftBumper, ControllerButton.RightBumper }, 0, 0, 0, 0, 0, 0);
        now = 20;
        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.LeftBumper, ControllerButton.RightBumper }, 0, 0, 0, 0, 0, 0);

        Assert.Single(state.ActiveMacros);
        Assert.Equal(MacroRepeatMode.HoldUntilRelease, state.ActiveMacros.Single().RepeatMode);
    }

    [Theory]
    [InlineData("A", "KeyTap:Space", "at least two")]
    [InlineData("A + Banana", "KeyTap:Space", "Banana")]
    [InlineData("A + A", "KeyTap:Space", "same button twice")]
    [InlineData("A + B", "", "Macro has no steps")]
    [InlineData("A + B", "Nonsense:1", "Unknown action")]
    public void BadCombos_AreRefusedWithAnExplanation(string buttons, string action, string expected)
    {
        var vm = New();
        vm.NewComboButtons = buttons;
        vm.NewComboAction = action;

        vm.AddComboCommand.Execute(null);

        Assert.Contains(expected, vm.ComboMessage);
        Assert.Empty(vm.SelectedProfile.Combos);
    }

    [Fact]
    public void ASequenceMayRepeatAButton()
    {
        var vm = New();
        vm.NewComboKindIndex = 1;
        vm.NewComboButtons = "Up Up Down Down";
        vm.NewComboAction = "OpenUrl:https://example.com";

        vm.AddComboCommand.Execute(null);

        Assert.Equal(4, vm.SelectedProfile.Combos.Single().Buttons.Count);
        Assert.Equal("Up, Up, Down, Down", vm.Combos.Single().Description);
    }

    [Fact]
    public void RemovingACombo_TakesItOutOfTheProfileAndTheList()
    {
        var vm = New();
        vm.NewComboButtons = "A B";
        vm.NewComboAction = "KeyTap:Space";
        vm.AddComboCommand.Execute(null);

        vm.RemoveComboCommand.Execute(vm.Combos[0]);

        Assert.Empty(vm.Combos);
        Assert.Empty(vm.SelectedProfile.Combos);
    }

    [Fact]
    public void EachProfileShowsItsOwnCombos_AndTheChordWindow()
    {
        var vm = New();
        var first = vm.SelectedProfile;
        vm.NewComboButtons = "A B";
        vm.NewComboAction = "KeyTap:Space";
        vm.AddComboCommand.Execute(null);
        vm.ChordWindowMs = 200;

        vm.NewProfileCommand.Execute(null);
        Assert.Empty(vm.Combos);
        Assert.Equal(new Profile().ChordWindowMs, vm.ChordWindowMs);

        vm.SelectedProfile = first;
        Assert.Single(vm.Combos);
        Assert.Equal(200, vm.ChordWindowMs);
        Assert.Equal(200, first.ChordWindowMs);
    }

    // ---- capturing from the pad -------------------------------------------------------------------------------------

    [Fact]
    public void ACapturedChord_IsTheButtonsHeldTogether_WhenTheyAreLetGo()
    {
        var capture = new ComboCapture();
        capture.Start(ComboKind.Chord, 0);

        Assert.Null(capture.Feed(new HashSet<ControllerButton> { ControllerButton.A }, 10));
        Assert.Null(capture.Feed(new HashSet<ControllerButton> { ControllerButton.A, ControllerButton.X }, 50));
        Assert.Null(capture.Feed(new HashSet<ControllerButton> { ControllerButton.X }, 80));
        var done = capture.Feed(new HashSet<ControllerButton>(), 100);

        Assert.Equal(new[] { ControllerButton.A, ControllerButton.X }, done);
        Assert.False(capture.IsCapturing);
    }

    [Fact]
    public void ACapturedSequence_KeepsTheOrderAndRepeats_AndEndsAfterAPause()
    {
        var capture = new ComboCapture();
        capture.Start(ComboKind.Sequence, 0);
        var up = new HashSet<ControllerButton> { ControllerButton.DpadUp };
        var down = new HashSet<ControllerButton> { ControllerButton.DpadDown };
        var none = new HashSet<ControllerButton>();

        capture.Feed(up, 10); capture.Feed(none, 60);
        capture.Feed(up, 100); capture.Feed(none, 150);
        capture.Feed(down, 200); capture.Feed(none, 250);
        Assert.Null(capture.Feed(none, 1000));

        var done = capture.Feed(none, 200 + ComboCapture.SequenceIdleMs);
        Assert.Equal(new[] { ControllerButton.DpadUp, ControllerButton.DpadUp, ControllerButton.DpadDown }, done);
    }

    [Fact]
    public void Capturing_CanBeCancelled_AndNothingHappensWhenNotCapturing()
    {
        var capture = new ComboCapture();
        Assert.Null(capture.Feed(new HashSet<ControllerButton> { ControllerButton.A }, 0));

        capture.Start(ComboKind.Chord, 0);
        capture.Cancel();
        Assert.Null(capture.Feed(new HashSet<ControllerButton>(), 100));
    }

    [Fact]
    public void TheCaptureButton_StartsAndCancels()
    {
        var vm = New();

        vm.CaptureComboButtonsCommand.Execute(null);
        Assert.True(vm.IsCapturingCombo);
        Assert.Contains("together", vm.ComboMessage);

        vm.CaptureComboButtonsCommand.Execute(null);
        Assert.False(vm.IsCapturingCombo);
    }
}
