using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class ArtViewTests
{
    private static (MainViewModel Vm, MainWindow Window, ArtView View) Open()
    {
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        vm.SelectedSkin = SkinKind.PlayStation5;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<ArtView>().Single();
        return (vm, window, view);
    }

    private static void Shot(MainWindow window, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }

    [AvaloniaFact]
    public void PlayStation5_ShowsTheDetailedDrawing()
    {
        var (vm, window, view) = Open();

        Assert.True(vm.ShowSkinArt);
        Assert.True(view.IsVisible);
        Assert.NotNull(view.Scene);
        Shot(window, "ps5-idle");
    }

    private static ButtonViewModel Pad(MainViewModel vm, ControllerButton b) => vm.ControllerButtons.First(x => x.Button == b);

    [AvaloniaFact]
    public void LiveInput_MovesTheDrawing()
    {
        var (vm, window, view) = Open();

        Pad(vm, ControllerButton.A).IsPressed = true;            // cross
        Pad(vm, ControllerButton.DpadUp).IsPressed = true;
        Pad(vm, ControllerButton.RightTrigger).AnalogValue = 1;
        Pad(vm, ControllerButton.LeftTrigger).AnalogValue = 0.5;
        var ls = Pad(vm, ControllerButton.LeftStickClick);
        ls.StickOffsetX = 12; ls.StickOffsetY = -6; ls.IsPressed = true;
        Pad(vm, ControllerButton.B).IsRemapped = true;
        view.Settle();

        Shot(window, "ps5-live");
    }

    /// <summary>The drawing alone on the app's background, at the given size, for looking at details.</summary>
    private static void Gallery(string name, int width, int height, Action<MainViewModel>? configure = null, Action<ArtView>? after = null)
    {
        string? dir = Environment.GetEnvironmentVariable("REWASD_SHOTS");
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), Fakes.TempProfiles());
        vm.SelectedSkin = SkinKind.PlayStation5;
        configure?.Invoke(vm);
        var view = new ArtView { Scene = vm.SkinArt, Sources = vm.ControllerButtons };
        var window = new Window { Width = width, Height = height, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161617")), Content = view, DataContext = vm };
        window.Show();
        view.Settle();
        after?.Invoke(view);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }

    [AvaloniaFact]
    public void Gallery_IdleAndPressed()
    {
        Gallery("art-idle", 2048, 1480);
        Gallery("art-pressed", 2048, 1480, vm =>
        {
            foreach (var b in new[] { ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y, ControllerButton.DpadLeft,
                         ControllerButton.LeftBumper, ControllerButton.RightBumper, ControllerButton.Guide, ControllerButton.View, ControllerButton.Menu, ControllerButton.RightStickClick })
                Pad(vm, b).IsPressed = true;
            Pad(vm, ControllerButton.RightTrigger).AnalogValue = 1;
            Pad(vm, ControllerButton.LeftTrigger).AnalogValue = 0.5;
            Pad(vm, ControllerButton.LeftStickClick).StickOffsetX = -9;
            Pad(vm, ControllerButton.LeftStickClick).StickOffsetY = 9;
            Pad(vm, ControllerButton.RightStickClick).StickOffsetX = 6;
            Pad(vm, ControllerButton.RightStickClick).StickOffsetY = -12;
        });
    }

    private static ReWASD.UI.Mac.Art.ArtView.PartSnapshot Look(ArtView v, ControllerButton b) => v.Inspect(b)!.Value;

    [AvaloniaFact]
    public void APressedButton_SinksAndGlows_AndAReleasedOneRests()
    {
        var (vm, _, view) = Open();
        var cross = Pad(vm, ControllerButton.A);

        Assert.True(Look(view, ControllerButton.A).Transform.IsIdentity);
        Assert.Equal(0, Look(view, ControllerButton.A).Glow);

        cross.IsPressed = true;
        view.Settle();
        var pressed = Look(view, ControllerButton.A);
        Assert.False(pressed.Transform.IsIdentity);
        Assert.True(pressed.Transform.M11 < 1);
        Assert.True(pressed.Glow > 0.5);

        cross.IsPressed = false;
        view.Settle();
        Assert.True(Look(view, ControllerButton.A).Transform.IsIdentity);
        Assert.Equal(0, Look(view, ControllerButton.A).Glow);
    }

    [AvaloniaFact]
    public void ATriggerFollowsTheAnalogValue_AndAStickFollowsThePosition()
    {
        var (vm, _, view) = Open();

        Pad(vm, ControllerButton.LeftTrigger).AnalogValue = 1;
        var ls = Pad(vm, ControllerButton.LeftStickClick);
        ls.StickOffsetX = 12;
        ls.StickOffsetY = 0;
        view.Settle();

        Assert.True(Look(view, ControllerButton.LeftTrigger).Transform.M32 > 10);   // pulled toward the player
        Assert.True(Look(view, ControllerButton.LeftStickClick).Transform.M31 > 10); // the cap leaned right
        Assert.True(Look(view, ControllerButton.RightStickClick).Transform.IsIdentity);
    }

    [AvaloniaFact]
    public void TheEditedPartHasAHalo_AndARemappedOneHasADot()
    {
        var (vm, _, view) = Open();

        Assert.True(Look(view, ControllerButton.A).Halo > 0.5);        // A is selected when the app opens
        Assert.Equal(0, Look(view, ControllerButton.B).Halo);

        vm.SelectButton(Pad(vm, ControllerButton.B));
        Pad(vm, ControllerButton.Y).IsRemapped = true;
        view.Settle();

        Assert.Equal(0, Look(view, ControllerButton.A).Halo);
        Assert.True(Look(view, ControllerButton.B).Halo > 0.5);
        Assert.True(Look(view, ControllerButton.Y).MarkerVisible);
        Assert.False(Look(view, ControllerButton.B).MarkerVisible);
    }

    /// <summary>Where a point of the drawing lands in the window.</summary>
    private static Avalonia.Point Screen(Window window, ArtView view, double x, double y)
    {
        var root = ((Avalonia.Controls.Viewbox)view.Child!).Child!;
        return Avalonia.VisualExtensions.TranslatePoint(root, new Avalonia.Point(x, y), window)!.Value;
    }

    [AvaloniaFact]
    public void ClickingAPart_SelectsIt_AndClickingTheBodyDoesNot()
    {
        var (vm, window, view) = Open();

        void Click(double x, double y)
        {
            var p = Screen(window, view, x, y);
            window.MouseDown(p, Avalonia.Input.MouseButton.Left);
            window.MouseUp(p, Avalonia.Input.MouseButton.Left);
        }

        Click(795.6, 213.9);   // triangle
        Assert.Equal(ControllerButton.Y, vm.SelectedButton!.Button);

        Click(232, 236);       // d-pad up
        Assert.Equal(ControllerButton.DpadUp, vm.SelectedButton!.Button);

        Click(513.8, 397);     // PS logo
        Assert.Equal(ControllerButton.Guide, vm.SelectedButton!.Button);

        Click(369, 403);       // left stick
        Assert.Equal(ControllerButton.LeftStickClick, vm.SelectedButton!.Button);

        Click(233, 100);       // the visible, raised left trigger
        Assert.Equal(ControllerButton.LeftTrigger, vm.SelectedButton!.Button);

        Click(233, 150);       // the bumper strip in front of it
        Assert.Equal(ControllerButton.LeftBumper, vm.SelectedButton!.Button);

        Click(140, 450);       // plain shell
        Assert.Equal(ControllerButton.LeftBumper, vm.SelectedButton!.Button);

        Click(513, 250);       // the touchpad
        Assert.Equal(ControllerButton.LeftBumper, vm.SelectedButton!.Button);
    }

    [AvaloniaFact]
    public void TheGlowOfOnePart_NeverStealsAClickFromAnother()
    {
        var (vm, window, view) = Open();
        vm.SelectButton(Pad(vm, ControllerButton.B));
        Pad(vm, ControllerButton.A).IsPressed = true;      // the cross's glow now spreads past the button's edge
        view.Settle();

        var p = Screen(window, view, 795.6, 346 + 36);     // just outside the cross, inside its glow
        window.MouseDown(p, Avalonia.Input.MouseButton.Left);
        window.MouseUp(p, Avalonia.Input.MouseButton.Left);

        Assert.Equal(ControllerButton.B, vm.SelectedButton!.Button);   // unchanged: the click landed on the shell
    }

    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void TheFrameLoop_EasesAPressIn_AndStopsWhenSettled()
    {
        var (vm, _, view) = Open();
        Pad(vm, ControllerButton.RightTrigger).AnalogValue = 1;

        Assert.Equal(0, Look(view, ControllerButton.RightTrigger).Glow);   // nothing has been drawn yet

        Frames(3);
        double early = Look(view, ControllerButton.RightTrigger).Glow;
        Assert.InRange(early, 0.0001, 0.349);                              // part-way: it eases rather than snaps

        Frames(30);
        double later = Look(view, ControllerButton.RightTrigger).Glow;
        Assert.True(later > early, "the trigger kept moving as frames arrived");

        view.Advance(2);                                                    // plenty of time for the spring to settle
        Assert.Equal(0.35, Look(view, ControllerButton.RightTrigger).Glow, 2);   // a trigger's tint tops out at 35%
    }

    [AvaloniaFact]
    public void ChangingTheSkin_RebuildsWithoutLeavingOldParts()
    {
        var (vm, window, view) = Open();

        vm.SelectedSkin = SkinKind.Xbox;
        vm.SelectedSkin = SkinKind.PlayStation5;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Pad(vm, ControllerButton.A).IsPressed = true;
        view.Settle();

        Assert.True(Look(view, ControllerButton.A).Glow > 0.5);
        Assert.Single(window.GetVisualDescendants().OfType<ArtView>());
    }

    [AvaloniaFact]
    public void Xbox_KeepsTheOutlineDrawing()
    {
        var (vm, _, view) = Open();

        vm.SelectedSkin = SkinKind.Xbox;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(vm.ShowSkinArt);
        Assert.False(view.IsVisible);
    }
}
