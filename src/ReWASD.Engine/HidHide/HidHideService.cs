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

/// <summary>
/// Hides physical controllers from other applications through the HidHide driver's CLI, so a game only
/// sees the virtual pad. This mutes the device as a whole: HidHide works per device, not per button.
/// </summary>
public sealed class HidHideService
{
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
    /// Hides the given devices and lets this app (and only this app) keep seeing them.
    /// Pass devices captured before the virtual pad connected so the virtual pad is never hidden.
    /// </summary>
    public bool Hide(IEnumerable<string> devicePaths)
    {
        if (_runner == null) return false;

        var paths = devicePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count == 0) return false;

        // Whitelist ourselves first, otherwise we would lose the input we are trying to remap
        if (!_registeredApp && !string.IsNullOrEmpty(_appPath))
        {
            if (_runner.Run(new[] { "--app-reg", _appPath }) == null) return false;
            _registeredApp = true;
        }

        foreach (var path in paths)
        {
            if (_runner.Run(new[] { "--dev-hide", path }) != null) _hidden.Add(path);
        }

        if (_hidden.Count == 0) return false;
        return _runner.Run(new[] { "--cloak-on" }) != null;
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
    public static IReadOnlyList<string> ParseDevicePaths(string json)
    {
        var result = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var group in doc.RootElement.EnumerateArray())
            {
                if (!group.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array) continue;

                foreach (var device in devices.EnumerateArray())
                {
                    if (device.TryGetProperty("present", out var present) && present.ValueKind == JsonValueKind.False) continue;
                    if (device.TryGetProperty("deviceInstancePath", out var path) && path.GetString() is { Length: > 0 } p)
                    {
                        result.Add(p);
                    }
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
