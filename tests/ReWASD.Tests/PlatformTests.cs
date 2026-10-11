using ReWASD.Core.Models;
using ReWASD.Engine.Platform;
using ReWASD.Engine.Platform.MacOS;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.Tests;

public class PlatformTests
{
    [Fact]
    public void ThePlatformInfo_MatchesTheOperatingSystem()
    {
        var info = PlatformServices.Current;

        Assert.Equal(OperatingSystem.IsWindows() ? "Windows" : "macOS", info.Name);
        Assert.Equal(OperatingSystem.IsWindows(), info.SupportsVirtualController);
        Assert.Equal(OperatingSystem.IsWindows(), info.SupportsDeviceHiding);
        Assert.Equal(OperatingSystem.IsMacOS(), info.NeedsInputPermissions);
    }

    [Fact]
    public void NullVirtualGamepad_NeverConnects_AndIgnoresReports()
    {
        using var pad = new NullVirtualGamepad();
        pad.Initialize();

        Assert.False(pad.IsDriverAvailable);
        Assert.False(pad.Connect(ControllerType.Xbox360));
        Assert.False(pad.IsConnected);
        Assert.Null(pad.ConnectedType);
        pad.SendReport(new ReWASD.Engine.Remapper.RemappedState());
        pad.DisconnectVirtual();
    }
}
