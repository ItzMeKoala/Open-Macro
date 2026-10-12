using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using ReWASD.Core.Models;
using ReWASD.Core.Skins.Art;
using ReWASD.UI.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Tests;

/// <summary>Runs a test body on a single-threaded-apartment thread, which WPF needs.</summary>
internal static class Sta
{
    public static void Run(Action body)
    {
        if (!OperatingSystem.IsWindows()) return; // WPF only runs on Windows

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

public class ArtViewTests
{
    private static List<ButtonViewModel> PadFor(ArtScene scene) =>
        scene.Parts.Select(p => new ButtonViewModel { Kind = SourceKind.Pad, Button = p.Button, SourceName = p.Button.ToString(), FriendlyName = p.Name }).ToList();

    private static ArtView Show(ArtScene scene, List<ButtonViewModel> pad)
    {
        var view = new ArtView { Scene = scene, Sources = pad };
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        return view;
    }

    private static Point ToView(ArtView view, double x, double y)
    {
        var root = ((Viewbox)view.Child).Child;
        return root.TransformToAncestor(view).Transform(new Point(x, y));
    }

    public static IEnumerable<object[]> Scenes() => new[]
    {
        new object[] { "DualSense" },
        new object[] { "Xbox" }
    };

    private static ArtScene Scene(string name) => name == "DualSense" ? ArtLibrary.DualSense : ArtLibrary.XboxOne;

    [Theory]
    [MemberData(nameof(Scenes))]
    public void TheDrawingBuilds_WithoutFailing_AndEveryPartIsThere(string name) => Sta.Run(() =>
    {
        var scene = Scene(name);
        var view = Show(scene, PadFor(scene));

        Assert.False(view.HasFailed);
        foreach (var part in scene.Parts) Assert.NotNull(view.Inspect(part.Button));
    });

    [Theory]
    [MemberData(nameof(Scenes))]
    public void APressedButton_SinksAndGlows_AndAReleasedOneRests(string name) => Sta.Run(() =>
    {
        var scene = Scene(name);
        var pad = PadFor(scene);
        var view = Show(scene, pad);
        var a = pad.First(p => p.Button == ControllerButton.A);

        Assert.True(view.Inspect(ControllerButton.A)!.Value.Transform.IsIdentity);

        a.IsPressed = true;
        view.Settle();
        var pressed = view.Inspect(ControllerButton.A)!.Value;
        Assert.False(pressed.Transform.IsIdentity);
        Assert.True(pressed.Glow > 0.5);

        a.IsPressed = false;
        view.Settle();
        Assert.True(view.Inspect(ControllerButton.A)!.Value.Transform.IsIdentity);
        Assert.Equal(0, view.Inspect(ControllerButton.A)!.Value.Glow);
    });

    [Theory]
    [MemberData(nameof(Scenes))]
    public void TriggersFollowTheAnalogValue_SticksFollowThePosition_SelectionAndRemapShow(string name) => Sta.Run(() =>
    {
        var scene = Scene(name);
        var pad = PadFor(scene);
        var view = Show(scene, pad);

        pad.First(p => p.Button == ControllerButton.LeftTrigger).AnalogValue = 1;
        var ls = pad.First(p => p.Button == ControllerButton.LeftStickClick);
        ls.StickOffsetX = 12;
        pad.First(p => p.Button == ControllerButton.Y).IsSelected = true;
        pad.First(p => p.Button == ControllerButton.B).IsRemapped = true;
        view.Settle();

        Assert.True(view.Inspect(ControllerButton.LeftTrigger)!.Value.Transform.OffsetY > 5);
        Assert.True(view.Inspect(ControllerButton.LeftStickClick)!.Value.Transform.OffsetX > 5);
        Assert.True(view.Inspect(ControllerButton.RightStickClick)!.Value.Transform.IsIdentity);
        Assert.True(view.Inspect(ControllerButton.Y)!.Value.Halo > 0.5);
        Assert.True(view.Inspect(ControllerButton.B)!.Value.MarkerVisible);
        Assert.False(view.Inspect(ControllerButton.A)!.Value.MarkerVisible);
    });

    [Fact]
    public void ClickingAPlayStationPart_FindsIt_AndTheBodyIsNotAButton() => Sta.Run(() =>
    {
        var scene = ArtLibrary.DualSense;
        var view = Show(scene, PadFor(scene));

        Assert.Equal(ControllerButton.A, view.ButtonAt(ToView(view, 795.6, 346)));
        Assert.Equal(ControllerButton.Y, view.ButtonAt(ToView(view, 795.6, 213.9)));
        Assert.Equal(ControllerButton.Guide, view.ButtonAt(ToView(view, 513.8, 397)));
        Assert.Equal(ControllerButton.LeftStickClick, view.ButtonAt(ToView(view, 369, 403)));
        Assert.Equal(ControllerButton.DpadUp, view.ButtonAt(ToView(view, 232, 236)));
        Assert.Null(view.ButtonAt(ToView(view, 140, 450)));   // plain shell
        Assert.Null(view.ButtonAt(ToView(view, 513, 250)));   // the touchpad
    });

    [Fact]
    public void ClickingAnXboxPart_FindsIt_AndTheBodyIsNotAButton() => Sta.Run(() =>
    {
        var scene = ArtLibrary.XboxOne;
        var view = Show(scene, PadFor(scene));

        Assert.Equal(ControllerButton.Y, view.ButtonAt(ToView(view, 1291.7, 267.1)));
        Assert.Equal(ControllerButton.X, view.ButtonAt(ToView(view, 1186.6, 365.3)));
        Assert.Equal(ControllerButton.Guide, view.ButtonAt(ToView(view, 900.3, 198)));
        Assert.Equal(ControllerButton.LeftStickClick, view.ButtonAt(ToView(view, 516, 363)));
        Assert.Equal(ControllerButton.DpadUp, view.ButtonAt(ToView(view, 702.7, 540)));
        Assert.Equal(ControllerButton.DpadLeft, view.ButtonAt(ToView(view, 640, 604)));
        Assert.Equal(ControllerButton.View, view.ButtonAt(ToView(view, 789.5, 365)));
        Assert.Null(view.ButtonAt(ToView(view, 400, 800)));   // plain body
        Assert.Null(view.ButtonAt(ToView(view, 900, 600)));
    });

    [Fact]
    public void ADrawingThatCannotBeBuilt_RaisesRenderFailed_InsteadOfThrowing() => Sta.Run(() =>
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g data-button="A"><path d="M 0 0 L nonsense" fill="#fff"/><path class="hit" d="M 0 0 L 10 10 L 10 0 Z"/></g>
            </svg>
            """;
        bool raised = false;
        var view = new ArtView();
        view.RenderFailed += (_, _) => raised = true;

        view.Scene = SvgArtParser.Parse(svg);

        Assert.True(raised);
        Assert.True(view.HasFailed);
    });

    [Fact]
    public void TheFramesEaseAPressIn_ThenStop() => Sta.Run(() =>
    {
        var scene = ArtLibrary.XboxOne;
        var pad = PadFor(scene);
        var view = Show(scene, pad);

        pad.First(p => p.Button == ControllerButton.RightTrigger).AnalogValue = 1;
        for (int i = 0; i < 4; i++) view.Advance(1.0 / 60);
        double early = view.Inspect(ControllerButton.RightTrigger)!.Value.Glow;
        Assert.InRange(early, 0.0001, 0.349);

        for (int i = 0; i < 60; i++) view.Advance(0.05);
        Assert.Equal(0.35, view.Inspect(ControllerButton.RightTrigger)!.Value.Glow, 2);
    });
}

/// <summary>The detailed keyboard on Windows: it builds, its keys follow the keyboard and clicking one selects it.</summary>
public class KeyboardArtViewTests
{
    private static List<ButtonViewModel> Keys() =>
        ReWASD.Core.Skins.InputLayouts.Keyboard.Select(k => new ButtonViewModel { Kind = SourceKind.Key, SourceName = k.Name, FriendlyName = k.Name }).ToList();

    [Fact]
    public void TheKeyboard_Builds_AndAKeySinksAndLightsWhileHeld() => Sta.Run(() =>
    {
        var keys = Keys();
        var view = new ArtView { Scene = ArtLibrary.Keyboard, Sources = keys };
        view.Measure(new Size(1100, 500));
        view.Arrange(new Rect(0, 0, 1100, 500));
        view.UpdateLayout();
        Assert.False(view.HasFailed);

        foreach (var k in keys) Assert.NotNull(view.Inspect(k.SourceName));

        keys.First(k => k.SourceName == "W").IsPressed = true;
        view.Settle();
        var w = view.Inspect("W")!.Value;
        Assert.True(w.Transform.OffsetY > 2);
        Assert.True(w.Glow > 0.2);
        Assert.Equal(0, view.Inspect("Q")!.Value.Glow);
    });

    [Fact]
    public void ClickingAKey_SelectsIt() => Sta.Run(() =>
    {
        var keys = Keys();
        var view = new ArtView { Scene = ArtLibrary.Keyboard, Sources = keys };
        view.Measure(new Size(1100, 500));
        view.Arrange(new Rect(0, 0, 1100, 500));
        view.UpdateLayout();

        var scene = ArtLibrary.Keyboard;
        var root = ((Viewbox)view.Child).Child;
        foreach (var name in new[] { "Escape", "Q", "Space", "Left", "PageDown" })
        {
            var b = scene.PartForKey(name)!.HitBounds;
            var at = root.TransformToAncestor(view).Transform(new Point(b.CenterX, b.CenterY));    // drawing units; the root moves the origin itself
            Assert.Equal(name, view.KeyAt(at));
        }

        // The case, and a key that cannot be remapped, are not keys
        var caps = KeyboardArt.Keys.First(k => k.Legend == "Caps Lock");
        var capsRect = KeyboardArt.CapOf(caps);
        Assert.Null(view.KeyAt(root.TransformToAncestor(view).Transform(new Point(capsRect.CenterX, capsRect.CenterY))));
        Assert.Null(view.KeyAt(root.TransformToAncestor(view).Transform(new Point(10, 10))));
    });
}

/// <summary>WPF's key names reach every key of the keyboard drawing.</summary>
public class WpfWindowKeyTests
{
    [Fact]
    public void EveryKeyOnTheDrawing_CanBeReachedFromAWpfKey()
    {
        var drawn = ReWASD.Core.Skins.InputLayouts.Keyboard.Select(k => k.Name).ToHashSet();
        var reachable = Enum.GetNames<System.Windows.Input.Key>().Select(WindowKeys.ToSourceName).Where(n => n != null).ToHashSet();

        Assert.All(drawn, n => Assert.Contains(n, reachable));
        Assert.All(reachable, n => Assert.Contains(n!, drawn));
        Assert.Equal("Enter", WindowKeys.ToSourceName(System.Windows.Input.Key.Return.ToString()));
        Assert.Equal("PageUp", WindowKeys.ToSourceName(System.Windows.Input.Key.PageUp.ToString()));
        Assert.Equal("PageDown", WindowKeys.ToSourceName(System.Windows.Input.Key.PageDown.ToString()));
    }
}
