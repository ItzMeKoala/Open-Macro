using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.UI.Mac.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

public class OverlayTests
{
    private static (MainViewModel Vm, MainWindow Window, Fakes.Pad Pad) Open()
    {
        var pad = new Fakes.Pad();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo, pad), new Fakes.Ui(), Fakes.TempProfiles());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (vm, window, pad);
    }

    [AvaloniaFact]
    public void TheOverlay_OpensAndClosesWithTheSwitch()
    {
        var (vm, window, _) = Open();
        Assert.Null(window.Overlay);

        vm.ShowOverlay = true;
        Assert.NotNull(window.Overlay);
        Assert.True(window.Overlay!.Topmost);
        Assert.False(window.Overlay.Art.ShowEditing);
        Assert.NotNull(window.Overlay.Art.Scene);                  // the controller picture is drawn, the same one as in the editor
        Assert.True(window.Overlay.Art.IsVisible);

        vm.ShowOverlay = true;                                    // nothing doubles up
        Assert.Single(window.GetVisualDescendants().OfType<ArtView>());

        vm.ShowOverlay = false;
        Assert.Null(window.Overlay);
    }

    [AvaloniaFact]
    public void ClosingTheOverlayItself_TurnsTheSwitchOff()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;

        window.Overlay!.Close();

        Assert.False(vm.ShowOverlay);
        Assert.Null(window.Overlay);
    }

    [AvaloniaFact]
    public void ClosingTheMainWindow_ClosesTheOverlayToo()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;
        var overlay = window.Overlay!;

        window.Close();

        Assert.Null(window.Overlay);
        Assert.False(overlay.IsVisible);
    }

    [AvaloniaFact]
    public void TheOverlay_FollowsTheSamePressedButtons()
    {
        var (vm, window, pad) = Open();
        vm.ShowOverlay = true;
        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.A }, 0, 0, 0, 0, 0, 0);

        vm.LiveTickForTests();

        Assert.True(vm.ControllerButtons.First(b => b.Button == ControllerButton.A).IsPressed);
        Assert.Equal("A", vm.PressedText);

        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.DpadUp, ControllerButton.LeftBumper }, 0, 0, 0, 0, 0, 0);
        vm.LiveTickForTests();
        Assert.Contains("LB", vm.PressedText);
        Assert.Contains("Up", vm.PressedText);

        pad.State = null;
        vm.LiveTickForTests();
        Assert.Equal("", vm.PressedText);
    }

    [AvaloniaFact]
    public void ASkinWithoutAPicture_ShowsThePressedButtonsAsText()
    {
        var (vm, window, pad) = Open();
        vm.SelectedSkin = SkinKind.PlayStation4;
        Assert.False(vm.OverlayHasArt);

        vm.ShowOverlay = true;
        Assert.False(window.Overlay!.Art.IsVisible);
        var text = window.Overlay.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.IsVisible);

        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.B }, 0, 0, 0, 0, 0, 0);
        vm.LiveTickForTests();
        Assert.Equal("B", text.Text);
    }

    [AvaloniaFact]
    public void WhileTheOverlayIsOff_NothingIsWorkedOut()
    {
        var (vm, _, pad) = Open();
        pad.State = new PhysicalState(new HashSet<ControllerButton> { ControllerButton.A }, 0, 0, 0, 0, 0, 0);

        vm.LiveTickForTests();

        Assert.Equal("", vm.PressedText);
    }

    [AvaloniaFact]
    public void EditingAPart_DoesNotGlowInTheOverlay_ButDoesInTheEditor()
    {
        var (vm, window, _) = Open();
        vm.ShowOverlay = true;
        vm.SelectButton(vm.ControllerButtons.First(b => b.Button == ControllerButton.X));

        var editor = window.GetVisualDescendants().OfType<ArtView>().Single();
        Assert.True(editor.ShowEditing);
        Assert.False(window.Overlay!.Art.ShowEditing);
    }
}
