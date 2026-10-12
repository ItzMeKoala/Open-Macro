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

/// <summary>The detailed mouse drawing, and the mouse tab that shows it.</summary>
public class MouseArtTests
{
    private static MainViewModel New() => new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
    private static ButtonViewModel Button(MainViewModel vm, string name) => vm.MouseSources.First(m => m.SourceName == name);

    [Fact]
    public void EveryMouseButton_IsExactlyOnePart()
    {
        var scene = ArtLibrary.Mouse;
        Assert.Equal(InputLayouts.Mouse.Count, scene.Parts.Count);
        foreach (var m in InputLayouts.Mouse) Assert.NotNull(scene.PartForKey(m.Name));
        Assert.All(scene.Parts, p => Assert.Equal(ControllerButton.None, p.Button));
    }

    [Fact]
    public void TheMainButtons_AreMirrorImages_SplitByTheSeam_AndStopAtThePalm()
    {
        Assert.Equal(MouseArt.LeftButton.Count, MouseArt.RightButton.Count);
        Assert.All(MouseArt.LeftButton, p => Assert.True(p.X <= 297.01, $"{p.X}"));
        Assert.All(MouseArt.RightButton, p => Assert.True(p.X >= 302.99, $"{p.X}"));
        Assert.All(MouseArt.LeftButton, p => Assert.True(p.Y <= MouseArt.PalmY(p.X) + 0.01));
        Assert.All(MouseArt.Palm, p => Assert.True(p.Y >= MouseArt.PalmY(p.X) - 0.01));
    }

    [Fact]
    public void TheWheelsThreeParts_DoNotOverlap()
    {
        Assert.Equal(MouseArt.WheelUpBand.Bottom, MouseArt.WheelPressBand.Top);
        Assert.Equal(MouseArt.WheelPressBand.Bottom, MouseArt.WheelDownBand.Top);
        var scene = ArtLibrary.Mouse;
        Assert.True(scene.PartForKey("Wheel Up")!.HitBounds.Bottom <= scene.PartForKey("Middle Click")!.HitBounds.Y + 0.01);
        Assert.True(scene.PartForKey("Middle Click")!.HitBounds.Bottom <= scene.PartForKey("Wheel Down")!.HitBounds.Y + 0.01);
    }

    [Fact]
    public void TheThumbButtons_SitOnTheLeftFlank_InsideTheBody()
    {
        var f = MouseArt.ThumbButton(forward: true);
        var b = MouseArt.ThumbButton(forward: false);
        Assert.True(f.Y + f.H < b.Y);                               // forward is in front of back
        foreach (var t in new[] { f, b })
        {
            Assert.True(t.X > MouseArt.LeftEdgeAt(t.Y) && t.X > MouseArt.LeftEdgeAt(t.Y + t.H));
            Assert.True(t.X < 160);
        }
    }

    [AvaloniaFact]
    public void TheMouseTab_ShowsTheDrawing_AndItsButtonsFollowTheMouse()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm, Width = 1300, Height = 800 };
        window.Show();
        vm.SelectedInputTab = InputTab.Mouse;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowMouseArt);
        Assert.False(vm.ShowOutline);

        var view = window.GetVisualDescendants().OfType<ArtView>().Single(v => v.Name == "MouseArt");
        Assert.True(view.IsEffectivelyVisible);
        Shot(window, "mouse-art");

        Button(vm, "Left Click").IsPressed = true;
        Button(vm, "Wheel Down").IsPressed = true;
        vm.SelectButton(Button(vm, "Mouse Button 4"));
        view.Settle();

        Assert.True(view.Inspect("Left Click")!.Value.Transform.M32 > 1);
        Assert.True(view.Inspect("Left Click")!.Value.Glow > 0.15);
        Assert.True(view.Inspect("Wheel Down")!.Value.Glow > 0.3);
        Assert.Equal(0, view.Inspect("Right Click")!.Value.Glow);
        Assert.True(view.Inspect("Mouse Button 4")!.Value.Halo > 0.9);

        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (!string.IsNullOrEmpty(dir)) window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "mouse-art-held.png"));
        vm.Shutdown();
    }

    [Fact]
    public void ButtonsAndTheWheel_SeenByTheWindow_ShowOnTheDrawing_WithRemappingOff()
    {
        var vm = New();
        vm.NoteWindowMouse("Right Click", true);
        vm.LiveTickForTests();
        Assert.True(Button(vm, "Right Click").IsPressed);
        vm.NoteWindowMouse("Right Click", false);
        Assert.False(Button(vm, "Right Click").IsPressed);

        vm.NoteWindowWheel(up: true);
        vm.LiveTickForTests();
        Assert.True(Button(vm, "Wheel Up").IsPressed);           // lit for a moment...
        Thread.Sleep(MainViewModel.WheelFlashMs + 60);
        vm.LiveTickForTests();
        Assert.False(Button(vm, "Wheel Up").IsPressed);          // ...then not

        vm.NoteWindowMouse("Mouse Button 4", true);
        vm.ReleaseWindowKeys();                                  // the window lost focus
        Assert.False(Button(vm, "Mouse Button 4").IsPressed);
        vm.Shutdown();
    }

    [AvaloniaFact]
    public void PressingTheRealMouseOverTheWindow_PressesTheDrawingsButton()
    {
        var vm = New();
        var window = new MainWindow { DataContext = vm, Width = 1300, Height = 800 };
        window.Show();
        vm.SelectedInputTab = InputTab.Mouse;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var at = new Avalonia.Point(600, 400);
        window.MouseDown(at, Avalonia.Input.MouseButton.Right);
        Assert.True(Button(vm, "Right Click").IsPressed);
        window.MouseUp(at, Avalonia.Input.MouseButton.Right);
        Assert.False(Button(vm, "Right Click").IsPressed);

        window.MouseWheel(at, new Avalonia.Vector(0, -1));
        Assert.True(Button(vm, "Wheel Down").IsPressed);
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
