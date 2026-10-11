using ReWASD.Engine.HidHide;

namespace ReWASD.Tests;

public class HidHideTests
{
    /// <summary>Behaves like the CLI: remembers cloak state and hidden devices, and can be told to fail commands.</summary>
    private sealed class FakeRunner : ICommandRunner
    {
        public List<string> Calls { get; } = new();
        public string DevGamingOutput { get; set; } = "[]";
        public string DevAllOutput { get; set; } = "[]";
        public HashSet<string> FailOn { get; } = new();
        private bool _cloak;
        private readonly List<string> _hiddenList = new();

        public string? Run(IReadOnlyList<string> arguments)
        {
            string line = string.Join(' ', arguments);
            Calls.Add(line);
            if (FailOn.Any(f => line.StartsWith(f))) return null;

            switch (arguments[0])
            {
                case "--dev-gaming": return DevGamingOutput;
                case "--dev-all": return DevAllOutput;
                case "--cloak-on": _cloak = true; return "";
                case "--cloak-off": _cloak = false; return "";
                case "--cloak-state": return _cloak ? "--cloak-on" : "--cloak-off";
                case "--dev-hide": _hiddenList.Add(arguments[1]); return "";
                case "--dev-unhide": _hiddenList.Remove(arguments[1]); return "";
                case "--dev-list": return string.Join(Environment.NewLine, _hiddenList.Select(h => $"--dev-hide \"{h}\""));
                default: return "";
            }
        }
    }

    private const string Sample = """
        [
          { "friendlyName": "Xbox Controller", "devices": [
              { "present": true,  "deviceInstancePath": "HID\\VID_045E&PID_02FD\\A", "baseContainerDeviceInstancePath": "BTHENUM\\DEV_1" },
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
    public void Parse_ReadsContainersAndGroups()
    {
        var devices = HidHideService.ParseDevices(Sample);

        var first = devices[0];
        Assert.Equal(@"BTHENUM\DEV_1", first.BaseContainer);
        Assert.Equal("Xbox Controller", first.Group);
        Assert.True(first.Present);
        Assert.False(devices[1].Present);
    }

    [Fact]
    public void Parse_GarbageOutput_ReturnsEmpty()
    {
        Assert.Empty(HidHideService.ParseDevicePaths("not json"));
        Assert.Empty(HidHideService.ParseDevicePaths("{}"));
    }

    [Fact]
    public void Hide_WhitelistsAppBeforeCloaking_VerifiesAndRestoreUndoesIt()
    {
        var runner = new FakeRunner();
        var service = new HidHideService(runner, @"C:\app\ReWASD.UI.exe");

        var result = service.Hide(new[] { "dev1" });
        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "dev1" }, result.Hidden);
        Assert.True(service.IsHiding);

        service.Restore();

        Assert.Equal(new[]
        {
            @"--app-reg C:\app\ReWASD.UI.exe",
            "--dev-hide dev1",
            "--cloak-on",
            "--cloak-state",
            "--dev-list",
            "--cloak-off",
            "--dev-unhide dev1",
            @"--app-unreg C:\app\ReWASD.UI.exe"
        }, runner.Calls);
        Assert.False(service.IsHiding);
    }

    [Fact]
    public void Hide_WithNoDevices_ExplainsWhyAndTouchesNothing()
    {
        var runner = new FakeRunner();
        var service = new HidHideService(runner, @"C:\app\x.exe");

        var result = service.Hide(Array.Empty<string>());

        Assert.False(result.Success);
        Assert.Contains("no physical controller", result.Message);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void Hide_WhenWhitelistingFails_DoesNotHideAnything()
    {
        var runner = new FakeRunner();
        runner.FailOn.Add("--app-reg");
        var service = new HidHideService(runner, @"C:\app\x.exe");

        var result = service.Hide(new[] { "dev1" });

        Assert.False(result.Success);
        Assert.DoesNotContain(runner.Calls, c => c.StartsWith("--dev-hide")); // never hide what we can't still read
    }

    [Fact]
    public void Hide_WhenSomeDevicesFail_ReportsThemButSucceeds()
    {
        var runner = new FakeRunner();
        runner.FailOn.Add("--dev-hide bad");
        var service = new HidHideService(runner, @"C:\app\x.exe");

        var result = service.Hide(new[] { "good", "bad" });

        Assert.True(result.Success);
        Assert.Equal(new[] { "good" }, result.Hidden);
        Assert.Equal(new[] { "bad" }, result.Failed);
        Assert.Contains("1 could not be hidden", result.Message);
    }

    [Fact]
    public void Hide_WhenEveryDeviceFails_IsAFailure()
    {
        var runner = new FakeRunner();
        runner.FailOn.Add("--dev-hide");
        var service = new HidHideService(runner, @"C:\app\x.exe");

        Assert.False(service.Hide(new[] { "a", "b" }).Success);
    }

    [Fact]
    public void PhysicalControllerDevices_IncludeSiblingsInTheSameContainer()
    {
        var runner = new FakeRunner
        {
            DevGamingOutput = """[ { "friendlyName": "Pad", "devices": [ { "deviceInstancePath": "HID\\PAD_A", "baseContainerDeviceInstancePath": "USB\\PAD" } ] } ]""",
            DevAllOutput = """
                [ { "friendlyName": "Pad", "devices": [
                    { "deviceInstancePath": "HID\\PAD_A", "baseContainerDeviceInstancePath": "USB\\PAD" },
                    { "deviceInstancePath": "USB\\IG_00", "baseContainerDeviceInstancePath": "USB\\PAD" } ] },
                  { "friendlyName": "Keyboard", "devices": [
                    { "deviceInstancePath": "HID\\KEYBOARD", "baseContainerDeviceInstancePath": "USB\\KB" } ] } ]
                """
        };
        var service = new HidHideService(runner, @"C:\app\x.exe");

        var devices = service.GetPhysicalControllerDevices();

        Assert.Equal(new[] { @"HID\PAD_A", @"USB\IG_00" }, devices);
    }

    [Fact]
    public void PhysicalControllerDevices_WithNoGamingDevice_IsEmpty()
    {
        var service = new HidHideService(new FakeRunner(), @"C:\app\x.exe");

        Assert.Empty(service.GetPhysicalControllerDevices());
    }

    [Fact]
    public void PhysicalControllerDevices_IgnoresAnImplausiblyLargeContainer()
    {
        var many = string.Join(",", Enumerable.Range(0, 40).Select(i => $$"""{ "deviceInstancePath": "X\\{{i}}", "baseContainerDeviceInstancePath": "USB\\HUB" }"""));
        var runner = new FakeRunner
        {
            DevGamingOutput = """[ { "devices": [ { "deviceInstancePath": "HID\\PAD", "baseContainerDeviceInstancePath": "USB\\HUB" } ] } ]""",
            DevAllOutput = $$"""[ { "devices": [ {{many}} ] } ]"""
        };
        var service = new HidHideService(runner, @"C:\app\x.exe");

        Assert.Equal(new[] { @"HID\PAD" }, service.GetPhysicalControllerDevices()); // hide only the pad, not a whole hub
    }

    [Fact]
    public void Diagnostics_ReportsEachCommand()
    {
        var text = new HidHideService(new FakeRunner(), @"C:\app\x.exe").Diagnostics();

        Assert.Contains("cloak:", text);
        Assert.Contains("gaming devices:", text);
        Assert.Contains("whitelisted apps:", text);
    }
}
