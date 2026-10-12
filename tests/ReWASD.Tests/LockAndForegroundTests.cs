using ReWASD.Core.Models;
using ReWASD.Engine.Foreground;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class ControllerLockTests
{
    private static (MappingEngine Engine, Func<long> Now, Action<long> Set) New(Profile profile)
    {
        long now = 0;
        var engine = new MappingEngine(profile, () => now) { IsActive = true };
        return (engine, () => now, t => now = t);
    }

    private static RemappedState Run(MappingEngine e, params ControllerButton[] pressed) =>
        e.ProcessInputs(new HashSet<ControllerButton>(pressed), 0, 0.9, 0, 0, 0.8, 0);

    private static Profile WithChord()
    {
        var p = Profile.CreateDefault();
        p.LockChord = new List<ControllerButton> { ControllerButton.View, ControllerButton.Menu };
        p.LockHoldMs = 1000;
        return p;
    }

    [Fact]
    public void HoldingTheChord_LocksAfterTheDelay_AndHoldingItAgainUnlocks()
    {
        var (engine, _, set) = New(WithChord());
        var changes = new List<bool>();
        engine.LockChanged += changes.Add;

        Run(engine, ControllerButton.View, ControllerButton.Menu);
        set(900); Run(engine, ControllerButton.View, ControllerButton.Menu);
        Assert.False(engine.IsLocked);

        set(1000); Run(engine, ControllerButton.View, ControllerButton.Menu);
        Assert.True(engine.IsLocked);

        set(5000); Run(engine, ControllerButton.View, ControllerButton.Menu);    // still held: no flip-flopping
        Assert.True(engine.IsLocked);

        set(5100); Run(engine);                                                  // let go
        set(6000); Run(engine, ControllerButton.View, ControllerButton.Menu);
        set(7000); Run(engine, ControllerButton.View, ControllerButton.Menu);
        Assert.False(engine.IsLocked);
        Assert.Equal(new[] { true, false }, changes);
    }

    [Fact]
    public void ReleasingEarly_DoesNotLock()
    {
        var (engine, _, set) = New(WithChord());

        Run(engine, ControllerButton.View, ControllerButton.Menu);
        set(600); Run(engine, ControllerButton.View, ControllerButton.Menu);
        set(700); Run(engine);
        set(1800); Run(engine, ControllerButton.View, ControllerButton.Menu);   // the timer started over

        Assert.False(engine.IsLocked);
    }

    [Fact]
    public void WhileLocked_NoButtonStickOrTriggerGetsThrough_ButKeysStillMap()
    {
        var profile = WithChord();
        profile.ShiftLayers[0].KeyboardMappings["Q"] = new ButtonMapping { Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "E" } };
        var (engine, _, _) = New(profile);
        engine.SetLocked(true);

        var state = engine.ProcessInputs(new HashSet<ControllerButton> { ControllerButton.A, ControllerButton.LeftBumper }, new[] { "Q" }, Array.Empty<string>(), 1, 1, 1, 1, 1, 1);

        Assert.Empty(state.ActiveVirtualButtons);
        Assert.Equal(0, state.LeftStickX);
        Assert.Equal(0, state.LeftTrigger);
        Assert.Contains("E", state.ActiveKeyboardKeys);

        engine.SetLocked(false);
        var open = Run(engine, ControllerButton.A);
        Assert.Contains(ControllerButton.A, open.ActiveVirtualButtons);
        Assert.Equal(0.8, open.LeftTrigger);
    }

    [Fact]
    public void TheChordButtons_AreNotPassedOnWhileHeld()
    {
        var (engine, _, _) = New(WithChord());

        var state = Run(engine, ControllerButton.View, ControllerButton.Menu, ControllerButton.A);

        Assert.Contains(ControllerButton.A, state.ActiveVirtualButtons);
        Assert.DoesNotContain(ControllerButton.View, state.ActiveVirtualButtons);
        Assert.DoesNotContain(ControllerButton.Menu, state.ActiveVirtualButtons);
    }

    [Fact]
    public void NoChord_NeverLocks_AndAHeldMappingEndsWhenLocked()
    {
        var (engine, _, set) = New(Profile.CreateDefault());
        for (long t = 0; t < 5000; t += 500) { set(t); Run(engine, ControllerButton.View, ControllerButton.Menu); }
        Assert.False(engine.IsLocked);

        Assert.Contains(ControllerButton.A, Run(engine, ControllerButton.A).ActiveVirtualButtons);
        engine.SetLocked(true);
        Assert.DoesNotContain(ControllerButton.A, Run(engine, ControllerButton.A).ActiveVirtualButtons);
    }
}

public class ForegroundTests
{
    private static ForegroundApp Win(string exe, int pid = 100) => new(exe, @"C:\Games\" + exe, pid);
    private static ForegroundApp Mac(string name, string bundle, int pid = 100) => new(name, bundle, pid);

    [Theory]
    [InlineData("game.exe", "game.exe", true)]
    [InlineData("GAME", "game.exe", true)]
    [InlineData("game.exe", "Game", true)]
    [InlineData("other.exe", "game.exe", false)]
    [InlineData("", "game.exe", false)]
    [InlineData("  ", "game.exe", false)]
    [InlineData("ga", "game.exe", false)]
    [InlineData("a.exe, game.exe", "game.exe", true)]
    [InlineData("a.exe; b.exe", "game.exe", false)]
    [InlineData("\"game.exe\"", "game.exe", true)]
    public void WindowsPrograms_AreMatchedByName(string pattern, string exe, bool expected) =>
        Assert.Equal(expected, AppMatcher.Matches(pattern, Win(exe)));

    [Theory]
    [InlineData("Safari", true)]
    [InlineData("safari", true)]
    [InlineData("com.apple.Safari", true)]
    [InlineData("Safari.app", true)]
    [InlineData("Chrome", false)]
    [InlineData("com.apple", false)]
    public void MacApps_AreMatchedByNameOrBundleId(string pattern, bool expected) =>
        Assert.Equal(expected, AppMatcher.Matches(pattern, Mac("Safari", "com.apple.Safari")));

    [Fact]
    public void APathPattern_MatchesTheProgramsFullPath()
    {
        Assert.True(AppMatcher.Matches(@"C:\Games\game.exe", Win("game.exe")));
        Assert.False(AppMatcher.Matches(@"D:\Games\game.exe", Win("game.exe")));
    }

    [Fact]
    public void NoApp_OrNoPattern_NeverMatches()
    {
        Assert.False(AppMatcher.Matches("game.exe", null));
        Assert.False(AppMatcher.Matches(null, Win("game.exe")));
    }

    private static Profile Linked(string name, string? process) => new() { Name = name, ProcessExecutable = process };

    [Fact]
    public void TheLinkedProfile_WinsWhileItsAppIsInFront_ElseTheManualOne()
    {
        var manual = Linked("manual", null);
        var game = Linked("game", "game.exe");
        var browser = Linked("browser", "chrome.exe");
        var all = new[] { manual, game, browser };

        Assert.Same(game, AppMatcher.Choose(all, Win("game.exe"), 1, manual, manual));
        Assert.Same(browser, AppMatcher.Choose(all, Win("Chrome.exe"), 1, manual, game));
        Assert.Same(manual, AppMatcher.Choose(all, Win("notepad.exe"), 1, manual, game));
        Assert.Same(manual, AppMatcher.Choose(all, null, 1, manual, game));
    }

    [Fact]
    public void OurOwnWindow_KeepsWhateverIsActive()
    {
        var manual = Linked("manual", null);
        var game = Linked("game", "game.exe");

        Assert.Same(game, AppMatcher.Choose(new[] { manual, game }, Win("ReWASD.exe", pid: 42), 42, manual, game));
        Assert.Same(manual, AppMatcher.Choose(new[] { manual, game }, Win("ReWASD.exe", pid: 42), 42, manual, null));
    }

    [Fact]
    public void ProfilesWithNoLink_AreNeverPickedAutomatically()
    {
        var manual = Linked("manual", null);
        var other = Linked("other", "");

        Assert.Same(manual, AppMatcher.Choose(new[] { manual, other }, Win("anything.exe"), 1, manual, manual));
    }

    [WindowsFact]
    public void Windows_CanSeeTheForegroundApp()
    {
        // A build agent may have no window in front, so only check that asking never throws
        var app = WindowsForeground.Current();
        if (app != null) Assert.False(string.IsNullOrEmpty(app.Name));
    }

    [MacFact]
    public void Mac_CanSeeTheForegroundApp()
    {
        var app = MacForeground.Current();
        if (app != null) Assert.False(string.IsNullOrEmpty(app.Name));
    }
}
