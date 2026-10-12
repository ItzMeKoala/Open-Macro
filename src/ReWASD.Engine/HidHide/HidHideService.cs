using System.Diagnostics;
using System.Text.Json;

namespace ReWASD.Engine.HidHide;

public interface ICommandRunner
{
    /// <summary>Runs the CLI with the given arguments and returns stdout, or null if it failed to run or exited non-zero.</summary>
    string? Run(IReadOnlyList<string> arguments);
}

internal sealed class ProcessCommandRunner : ICommandRunner
{
    private readonly string _exePath;

    public ProcessCommandRunner(string exePath)
    {
        _exePath = exePath;
    }

    public string? Run(IReadOnlyList<string> arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(_exePath)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in arguments) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null) return null;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>One device as reported by HidHide.</summary>
public sealed record HidHideDevice(string InstancePath, string BaseContainer, bool Present, string Group);

/// <summary>What happened when hiding was attempted. <see cref="Message"/> is written for the person using the app.</summary>
public sealed record HideResult(bool Success, IReadOnlyList<string> Hidden, IReadOnlyList<string> Failed, string Message)
{
    public static HideResult Fail(string message) => new(false, Array.Empty<string>(), Array.Empty<string>(), message);
}

/// <summary>
/// Hides physical controllers from other applications through the HidHide driver's CLI, so a game only
/// sees the virtual pad. This mutes the device as a whole: HidHide works per device, not per button.
/// </summary>
public sealed class HidHideService
{
    // A controller is one physical device; if "siblings" ever looks bigger than this, something is wrong with the match
    private const int MaxSiblingDevices = 16;

    private static readonly string[] DefaultCliPaths =
    {
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\Nefarius Software Solutions\HidHide\HidHideCLI.exe"
    };

    private readonly ICommandRunner? _runner;
    private readonly List<string> _hidden = new();
    private bool _registeredApp;
    private readonly string _appPath;

    public HidHideService(ICommandRunner? runner = null, string? appPath = null)
    {
        _appPath = appPath ?? Environment.ProcessPath ?? string.Empty;

        if (runner != null)
        {
            _runner = runner;
            return;
        }

        var cli = DefaultCliPaths.FirstOrDefault(File.Exists);
        if (cli != null) _runner = new ProcessCommandRunner(cli);
    }

    public bool IsAvailable => _runner != null;

    public bool IsHiding => _hidden.Count > 0;

    /// <summary>Device instance paths of all gaming devices currently present.</summary>
    public IReadOnlyList<string> GetPresentGamingDevices()
    {
        string? json = _runner?.Run(new[] { "--dev-gaming" });
        return json == null ? Array.Empty<string>() : ParseDevicePaths(json);
    }

    /// <summary>
    /// Every present device that belongs to a connected controller: the gaming devices HidHide reports plus
    /// anything else in the same physical device container (a pad often exposes several HID interfaces, and
    /// leaving one visible keeps the controller visible to games).
    /// </summary>
    public IReadOnlyList<string> GetPhysicalControllerDevices()
    {
        if (_runner == null) return Array.Empty<string>();

        string? gamingJson = _runner.Run(new[] { "--dev-gaming" });
        var gaming = gamingJson == null ? new List<HidHideDevice>() : ParseDevices(gamingJson).Where(d => d.Present).ToList();
        if (gaming.Count == 0) return Array.Empty<string>();

        var paths = new List<string>(gaming.Select(d => d.InstancePath));

        var containers = gaming.Select(d => d.BaseContainer).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (containers.Count > 0)
        {
            string? allJson = _runner.Run(new[] { "--dev-all" });
            if (allJson != null)
            {
                var siblings = ParseDevices(allJson).Where(d => d.Present && containers.Contains(d.BaseContainer)).ToList();
                if (siblings.Count > 0 && siblings.Count <= MaxSiblingDevices)
                {
                    foreach (var s in siblings) paths.Add(s.InstancePath);
                }
            }
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Hides the given devices and lets this app (and only this app) keep seeing them.
    /// Pass devices captured before the virtual pad connected so the virtual pad is never hidden.
    /// </summary>
    public HideResult Hide(IEnumerable<string> devicePaths)
    {
        if (_runner == null) return HideResult.Fail("HidHide is not installed.");

        var paths = devicePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count == 0)
        {
            return HideResult.Fail("HidHide found no physical controller to hide. Connect it before pressing Apply " +
                                   "(wireless-adapter dongles can't be hidden by HidHide).");
        }

        // Whitelist ourselves first, otherwise we would lose the input we are trying to remap
        if (!_registeredApp && !string.IsNullOrEmpty(_appPath))
        {
            if (_runner.Run(new[] { "--app-reg", _appPath }) == null)
                return HideResult.Fail("HidHide refused to whitelist this app, so the controller was not hidden.");
            _registeredApp = true;
        }

        var failed = new List<string>();
        foreach (var path in paths)
        {
            if (_runner.Run(new[] { "--dev-hide", path }) != null)
            {
                if (!_hidden.Contains(path, StringComparer.OrdinalIgnoreCase)) _hidden.Add(path);
            }
            else failed.Add(path);
        }

        if (_hidden.Count == 0)
            return new HideResult(false, Array.Empty<string>(), failed, "HidHide rejected every device, so the controller was not hidden.");

        if (_runner.Run(new[] { "--cloak-on" }) == null)
            return new HideResult(false, _hidden.ToList(), failed, "Devices were marked hidden but HidHide would not switch hiding on.");

        // Read the driver's state back instead of trusting the commands
        string? state = _runner.Run(new[] { "--cloak-state" });
        bool cloakOn = state == null || state.Contains("--cloak-on", StringComparison.OrdinalIgnoreCase); // an old CLI may not answer

        string? listed = _runner.Run(new[] { "--dev-list" });
        int confirmed = listed == null ? _hidden.Count : _hidden.Count(h => listed.Contains(h, StringComparison.OrdinalIgnoreCase));

        if (!cloakOn)
            return new HideResult(false, _hidden.ToList(), failed, "HidHide reports hiding is still off.");

        string message = $"Hid {_hidden.Count} device{(_hidden.Count == 1 ? "" : "s")} from other apps";
        if (confirmed < _hidden.Count) message += $" ({_hidden.Count - confirmed} not confirmed by HidHide)";
        if (failed.Count > 0) message += $"; {failed.Count} could not be hidden";
        return new HideResult(true, _hidden.ToList(), failed, message + ".");
    }

    /// <summary>A text dump of what HidHide currently has configured, for troubleshooting.</summary>
    public string Diagnostics()
    {
        if (_runner == null) return "HidHide CLI not found.";

        string Run(params string[] args) => _runner.Run(args)?.Trim() ?? "(command failed)";
        return string.Join(Environment.NewLine, new[]
        {
            "cloak: " + Run("--cloak-state"),
            "inverse: " + Run("--inv-state"),
            "gaming devices: " + Run("--dev-gaming"),
            "hidden devices: " + Run("--dev-list"),
            "whitelisted apps: " + Run("--app-list")
        });
    }

    /// <summary>Undoes everything Hide did. Safe to call when nothing is hidden.</summary>
    public void Restore()
    {
        if (_runner == null) return;

        if (_hidden.Count > 0) _runner.Run(new[] { "--cloak-off" });
        foreach (var path in _hidden) _runner.Run(new[] { "--dev-unhide", path });
        _hidden.Clear();

        if (_registeredApp)
        {
            _runner.Run(new[] { "--app-unreg", _appPath });
            _registeredApp = false;
        }
    }

    /// <summary>Extracts present devices' instance paths from the output of <c>HidHideCLI --dev-gaming</c>.</summary>
    public static IReadOnlyList<string> ParseDevicePaths(string json) =>
        ParseDevices(json).Where(d => d.Present).Select(d => d.InstancePath).ToList();

    /// <summary>Parses the grouped device list printed by <c>--dev-gaming</c> and <c>--dev-all</c>.</summary>
    public static IReadOnlyList<HidHideDevice> ParseDevices(string json)
    {
        var result = new List<HidHideDevice>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var group in doc.RootElement.EnumerateArray())
            {
                if (!group.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array) continue;
                string groupName = group.TryGetProperty("friendlyName", out var fn) ? fn.GetString() ?? "" : "";

                foreach (var device in devices.EnumerateArray())
                {
                    bool present = !(device.TryGetProperty("present", out var p) && p.ValueKind == JsonValueKind.False);
                    string? path = device.TryGetProperty("deviceInstancePath", out var pe) ? pe.GetString() : null;
                    if (string.IsNullOrEmpty(path)) continue;

                    string container = device.TryGetProperty("baseContainerDeviceInstancePath", out var bc) ? bc.GetString() ?? "" : "";
                    result.Add(new HidHideDevice(path, container, present, groupName));
                }
            }
        }
        catch (JsonException)
        {
            // Unexpected CLI output: treat as no devices rather than hiding the wrong thing
            result.Clear();
        }
        return result;
    }
}
