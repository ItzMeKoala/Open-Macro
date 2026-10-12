using System.Windows;
using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.Engine.VirtualOutput;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Tests;

/// <summary>The overlay window of the WPF front end: a borderless, transparent, always-on-top window showing the controller.</summary>
public class OverlayWindowTests
{
    private sealed class NoPad : IPhysicalController
    {
        public PhysicalState? Read() => null;
        public int ExcludedUserIndex { get; set; } = -1;
        public int ActiveUserIndex => -1;
        public IReadOnlyList<int> ConnectedSlots() => Array.Empty<int>();
        public string DescribeSlot(int slot) => "none";
    }

    private sealed class NullBackend : IInputBackend
    {
        public void KeyDown(ushort virtualKey) { }
        public void KeyUp(ushort virtualKey) { }
        public void MouseDown(MouseButtonKind button) { }
        public void MouseUp(MouseButtonKind button) { }
        public void MouseWheel(int delta) { }
        public void MouseMove(int dx, int dy) { }
    }

    private sealed class NullHook : IInputHook
    {
        public bool IsInstalled { get; private set; }
        public bool IncludeInjected { get; set; }
        public event Action? PanicRequested { add { } remove { } }
        public event Action<string, bool>? KeyEvent { add { } remove { } }
        public event Action<string, bool>? MouseEvent { add { } remove { } }
        public bool Start() => IsInstalled = true;
        public void Stop() => IsInstalled = false;
        public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot() => (Array.Empty<string>(), Array.Empty<string>());
        public void Dispose() { }
    }

    internal static MainViewModel NewViewModel() => new(
        new PlatformBackends
        {
            Info = PlatformServices.WindowsInfo,
            Reader = new NoPad(),
            InputBackend = new NullBackend(),
            Rumble = new NullRumbleOutput(),
            VirtualPad = new NullVirtualGamepad(),
            CreateHook = (_, _) => new NullHook()
        },
        new SynchronizationContextUi(),
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rewasd-wpf-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void TheOverlay_IsABorderlessTransparentTopmostWindow_ShowingTheControllerPicture() => Sta.Run(() =>
    {
        var vm = NewViewModel();
        var overlay = new OverlayWindow(vm);

        Assert.True(overlay.Topmost);
        Assert.True(overlay.AllowsTransparency);
        Assert.Equal(WindowStyle.None, overlay.WindowStyle);
        Assert.False(overlay.ShowInTaskbar);
        Assert.False(overlay.Art.ShowEditing);
        Assert.False(overlay.Art.IsHitTestVisible);       // clicks drag the window instead
        Assert.NotNull(overlay.Art.Scene);                // the same drawing as the editor
        Assert.Equal(Visibility.Visible, overlay.Art.Visibility);

        overlay.Measure(new Size(420, 300));
        overlay.Arrange(new Rect(0, 0, 420, 300));
        overlay.UpdateLayout();
        Assert.False(overlay.Art.HasFailed);
    });

    [Fact]
    public void ASkinWithoutAPicture_HidesTheDrawing_AndShowsText() => Sta.Run(() =>
    {
        var vm = NewViewModel();
        vm.SelectedSkin = ReWASD.Core.Skins.SkinKind.PlayStation4;

        var overlay = new OverlayWindow(vm);

        Assert.Equal(Visibility.Collapsed, overlay.Art.Visibility);
    });

    [Fact]
    public void TheCommandWheel_DrawsItsSlices_AndHighlightsTheChosenOne() => Sta.Run(() =>
    {
        var vm = NewViewModel();
        foreach (var (label, action) in new[] { ("Browser", "LaunchApp:notepad.exe"), ("Notes", "KeyTap:A"), ("Music", "KeyTap:B") })
        {
            vm.NewWheelLabel = label;
            vm.NewWheelAction = action;
            vm.AddWheelItemCommand.Execute(null);
        }

        var wheel = new WheelWindow(vm);

        Assert.True(wheel.Topmost);
        Assert.False(wheel.ShowActivated);        // it must never take the keyboard from a game
        Assert.True(wheel.AllowsTransparency);
        Assert.Equal(3, wheel.SliceCount);
        Assert.Equal(-1, wheel.DrawnSelection);

        vm.WheelSelectedIndex = 2;
        Assert.Equal(2, wheel.DrawnSelection);
        Assert.Equal("Music", wheel.CentreText);

        vm.WheelSelectedIndex = -1;
        Assert.Equal(-1, wheel.DrawnSelection);

        wheel.Measure(new Size(380, 380));
        wheel.Arrange(new Rect(0, 0, 380, 380));
        wheel.UpdateLayout();
    });
}
