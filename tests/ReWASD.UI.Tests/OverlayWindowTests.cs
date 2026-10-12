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

/// <summary>The drawing controls of the full-screen tester: they need no app resources, so they can be checked on their own.</summary>
public class TesterControlTests
{
    private static ReWASD.Engine.Input.PhysicalState Pad(double lx, double ly) =>
        new(new HashSet<ReWASD.Core.Models.ControllerButton>(), lx, ly, 0, 0, 0, 0);

    private static ReWASD.UI.Tester.TesterSnapshot CircleSnapshot()
    {
        var model = new ReWASD.UI.Tester.TesterModel();
        for (int deg = 0; deg < 360; deg += 2)
        {
            double a = deg * Math.PI / 180;
            model.Feed(Pad(Math.Cos(a), Math.Sin(a)), deg * 2);
        }
        return model.Snapshot();
    }

    [Fact]
    public void TheStickPlot_PlacesTheMeasuredShapeOnTheCircle() => Sta.Run(() =>
    {
        var points = ReWASD.UI.TesterControls.StickPlot.ShapePoints(CircleSnapshot().Left, new Point(100, 100), 80);

        Assert.Equal(ReWASD.UI.Tester.TesterModel.AngleBins, points.Count);
        Assert.All(points, p => Assert.Equal(80, Math.Sqrt((p.X - 100) * (p.X - 100) + (p.Y - 100) * (p.Y - 100)), 1));
        Assert.True(points[0].Y < 100);          // screen y goes down, the stick's goes up
    });

    [Fact]
    public void ThePlotsDraw_WithoutFailing() => Sta.Run(() =>
    {
        var snapshot = CircleSnapshot();
        var plot = new ReWASD.UI.TesterControls.StickPlot { Snapshot = snapshot.Left, DeadzoneMin = 0.1, DeadzoneMax = 0.9, Width = 260, Height = 260 };
        var graph = new ReWASD.UI.TesterControls.ScopeGraph { Series = snapshot.Scope, Channels = "0,1", Width = 400, Height = 150 };

        foreach (FrameworkElement element in new FrameworkElement[] { plot, graph })
        {
            element.Measure(new Size(element.Width, element.Height));
            element.Arrange(new Rect(0, 0, element.Width, element.Height));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)element.Width, (int)element.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(element);                                   // runs OnRender
            Assert.Equal((int)element.Width, bitmap.PixelWidth);
        }
    });
}
