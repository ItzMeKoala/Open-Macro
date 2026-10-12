using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Core.Skins.Art;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

/// <summary>The detailed keyboard drawing, and the keyboard tab that shows it.</summary>
public class KeyboardArtTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());

    [Fact]
    public void EveryRemappableKey_IsExactlyOnePart_AndEveryPartIsARemappableKey()
    {
        var scene = ArtLibrary.Keyboard;
        var names = InputLayouts.Keyboard.Select(k => k.Name).ToList();

        Assert.Equal(names.Count, scene.Parts.Count);
        foreach (var name in names) Assert.NotNull(scene.PartForKey(name));
        Assert.Equal(scene.Parts.Count, scene.Parts.Select(p => p.Key!.ToLowerInvariant()).Distinct().Count());
        Assert.All(scene.Parts, p =>
        {
            Assert.Equal(ControllerButton.None, p.Button);
            Assert.Equal(ArtMotion.Press, p.Motion);
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
        });
    }

    [Fact]
    public void TheBoard_IsAFullTenkeyless_WithNoKeysOverlapping()
    {
        var keys = KeyboardArt.Keys;
        Assert.Equal(87, keys.Count);                       // a standard ANSI tenkeyless board

        var caps = keys.Select(KeyboardArt.CapOf).ToList();
        for (int i = 0; i < caps.Count; i++)
        for (int j = i + 1; j < caps.Count; j++)
        {
            var a = caps[i];
            var b = caps[j];
            bool overlap = a.X < b.Right - 0.01 && b.X < a.Right - 0.01 && a.Y < b.Bottom - 0.01 && b.Y < a.Bottom - 0.01;
            Assert.False(overlap, $"{keys[i].Legend} overlaps {keys[j].Legend}");
        }
    }

    [Fact]
    public void TheLegends_AreText_ReadFromTheDrawing()
    {
        var texts = new List<ArtText>();
        void Walk(ArtGroup g)
        {
            foreach (var c in g.Children)
            {
                if (c is ArtText t) texts.Add(t);
                else if (c is ArtGroup sub) Walk(sub);
            }
        }
        Walk(ArtLibrary.Keyboard.Root);

        Assert.Contains(texts, t => t.Text == "Esc" && t.Anchor == ArtTextAnchor.Middle && t.Central);
        Assert.Contains(texts, t => t.Text == "Q");
        Assert.Contains(texts, t => t.Text == "!");                    // the shifted symbol above 1
        Assert.Contains(texts, t => t.Text == "Caps Lock" && t.Anchor == ArtTextAnchor.Start);
        Assert.Contains(texts, t => t.Text == "\\");
    }

    [Fact]
    public void TextIsReadFromSvg_WithItsAnchorAndCentre()
    {
        var scene = SvgArtParser.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <text x="50" y="40" font-size="20" font-weight="600" fill="#FFFFFF" text-anchor="middle" dominant-baseline="central">A &amp; B</text>
              <text x="10" y="90" font-size="10">plain</text>
            </svg>
            """);
        var a = (ArtText)scene.Root.Children[0];
        Assert.Equal("A & B", a.Text);
        Assert.Equal(ArtTextAnchor.Middle, a.Anchor);
        Assert.Equal(600, a.FontWeight);
        Assert.Equal(40, a.CenterY);

        var b = (ArtText)scene.Root.Children[1];
        Assert.Equal(ArtTextAnchor.Start, b.Anchor);
        Assert.Equal(90 - 3.5, b.CenterY, 3);                           // a baseline, moved up to the middle of the line
    }

    [AvaloniaFact]
    public void TheKeyboardTab_ShowsTheDrawing_AndItsKeysFollowTheKeyboard()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm, Width = 1300, Height = 800 };
        window.Show();

        vm.SelectedInputTab = InputTab.Keyboard;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowKeyboardArt);
        Assert.False(vm.ShowOutline);

        var view = window.FindControl<ArtView>("KeyboardArt") ?? window.GetVisualDescendants().OfType<ArtView>().Single(v => v.Name == "KeyboardArt");
        Assert.True(view.IsEffectivelyVisible);

        // Held down: it sinks and lights; selected: it gets the halo; remapped: the dot
        var w = vm.KeyboardSources.First(s => s.SourceName == "W");
        w.IsPressed = true;
        vm.SelectButton(vm.KeyboardSources.First(s => s.SourceName == "Space"));
        view.Settle();

        var held = view.Inspect("W")!.Value;
        Assert.True(held.Transform.M32 > 2);
        Assert.True(held.Glow > 0.3);
        Assert.True(view.Inspect("Space")!.Value.Halo > 0.9);
        Assert.Equal(0, view.Inspect("A")!.Value.Glow);

        Shot(window, "keyboard-art");

        // A picture of keys held down, taken straight away (the live display would let go of them: no hook runs in tests)
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (!string.IsNullOrEmpty(dir))
        {
            vm.SelectButton(vm.KeyboardSources.First(s => s.SourceName == "Space"));
            foreach (var k in new[] { "W", "ShiftKey", "Space" }) vm.KeyboardSources.First(s => s.SourceName == k).IsPressed = true;
            view.Settle();
            window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "keyboard-art-held.png"));
        }

        w.IsPressed = false;
        view.Settle();
        Assert.Equal(0, view.Inspect("W")!.Value.Transform.M32, 3);

        // Back on the gamepad the keyboard drawing is gone and the controller is back
        vm.SelectedInputTab = InputTab.Gamepad;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(view.IsEffectivelyVisible);
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void ClickingAKey_SelectsIt()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm, Width = 1300, Height = 800 };
        window.Show();
        vm.SelectedInputTab = InputTab.Keyboard;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<ArtView>().Single(v => v.Name == "KeyboardArt");
        var scene = ArtLibrary.Keyboard;
        foreach (var name in new[] { "Escape", "Q", "Enter", "Space", "Left", "PageDown" })
        {
            var b = scene.PartForKey(name)!.HitBounds;
            var canvas = view.GetVisualDescendants().OfType<Viewbox>().First().Child!;
            var point = Avalonia.VisualExtensions.TranslatePoint(canvas, new Avalonia.Point(b.CenterX, b.CenterY), window)!.Value;     // drawing units; the root moves the origin itself
            window.MouseDown(point, Avalonia.Input.MouseButton.Left);
            window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            Assert.Equal(name, vm.SelectedButton!.SourceName);
        }

        // A key that cannot be remapped selects nothing
        var caps = KeyboardArt.CapOf(KeyboardArt.Keys.First(k => k.Legend == "Caps Lock"));
        var root = view.GetVisualDescendants().OfType<Viewbox>().First().Child!;
        var at = Avalonia.VisualExtensions.TranslatePoint(root, new Avalonia.Point(caps.CenterX, caps.CenterY), window)!.Value;
        window.MouseDown(at, Avalonia.Input.MouseButton.Left);
        window.MouseUp(at, Avalonia.Input.MouseButton.Left);
        Assert.Equal("PageDown", vm.SelectedButton!.SourceName);
        vm.Shutdown();
    }

    private static void Shot(Window window, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }
}
