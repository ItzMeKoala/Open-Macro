using ReWASD.Engine.HidHide;

namespace ReWASD.Tests;

public class HidHideTests
{
    private sealed class FakeRunner : ICommandRunner
    {
        public List<string> Calls { get; } = new();
        public string DevGamingOutput { get; set; } = "[]";

        public string? Run(IReadOnlyList<string> arguments)
        {
            Calls.Add(string.Join(' ', arguments));
            return arguments[0] == "--dev-gaming" ? DevGamingOutput : string.Empty;
        }
    }

    private const string Sample = """
        [
          { "friendlyName": "Xbox Controller", "devices": [
              { "present": true,  "deviceInstancePath": "HID\\VID_045E&PID_02FD\\A" },
              { "present": false, "deviceInstancePath": "HID\\VID_054C&PID_09CC\\B" } ] },
          { "friendlyName": "Other", "devices": [ { "deviceInstancePath": "USB\\VID_1\\C" } ] }
        ]
        """;

    [Fact]
    public void Parse_ReturnsOnlyPresentDevices()
    {
        var paths = HidHideService.ParseDevicePaths(Sample);

        Assert.Equal(new[] { @"HID\VID_045E&PID_02FD\A", @"USB\VID_1\C" }, paths);
    }

    [Fact]
    public void Parse_GarbageOutput_ReturnsEmpty()
    {
        Assert.Empty(HidHideService.ParseDevicePaths("not json"));
        Assert.Empty(HidHideService.ParseDevicePaths("{}"));
    }

    [Fact]
    public void Hide_WhitelistsAppBeforeCloaking_AndRestoreUndoesIt()
    {
        var runner = new FakeRunner();
        var service = new HidHideService(runner, @"C:\app\ReWASD.UI.exe");

        Assert.True(service.Hide(new[] { "dev1" }));
        service.Restore();

        Assert.Equal(new[]
        {
            @"--app-reg C:\app\ReWASD.UI.exe",
            "--dev-hide dev1",
            "--cloak-on",
            "--cloak-off",
            "--dev-unhide dev1",
            @"--app-unreg C:\app\ReWASD.UI.exe"
        }, runner.Calls);
        Assert.False(service.IsHiding);
    }

    [Fact]
    public void Hide_WithNoDevices_DoesNothing()
    {
        var runner = new FakeRunner();
        var service = new HidHideService(runner, @"C:\app\x.exe");

        Assert.False(service.Hide(Array.Empty<string>()));
        Assert.Empty(runner.Calls);
    }
}
