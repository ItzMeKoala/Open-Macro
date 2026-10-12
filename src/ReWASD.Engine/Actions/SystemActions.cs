using System.Diagnostics;
using System.Net.Http;
using System.Text;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Actions;

/// <summary>Runs system actions for real on Windows and macOS.</summary>
public sealed class SystemActions : ISystemActions
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly Func<ProcessStartInfo, bool> _start;
    private readonly HttpClient _http;
    private readonly Func<ObsSettings> _obs;

    /// <param name="start">Starts a process; tests pass a recorder. Returns false if nothing was started.</param>
    public SystemActions(Func<ObsSettings>? obs = null, HttpClient? http = null, Func<ProcessStartInfo, bool>? start = null)
    {
        _obs = obs ?? (() => new ObsSettings());
        _http = http ?? SharedHttp;
        _start = start ?? (psi =>
        {
            using var process = Process.Start(psi);
            return process != null;
        });
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is "file" or "javascript" or "data") return;
        Run(OpenCommand(uri.ToString()));
    }

    public void LaunchApp(string appOrPath)
    {
        if (string.IsNullOrWhiteSpace(appOrPath)) return;
        Run(LaunchCommand(appOrPath.Trim()));
    }

    public void RunShell(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        Run(ShellCommand(command));
    }

    public async Task SendWebhookAsync(WebhookRequest request, CancellationToken cancellation)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

        if (request.Body.Length > 0 && request.Method != "GET")
        {
            string type = request.Body.TrimStart().StartsWith('{') || request.Body.TrimStart().StartsWith('[') ? "application/json" : "text/plain";
            message.Content = new StringContent(request.Body, Encoding.UTF8, type);
        }

        foreach (var (name, value) in request.Headers)
        {
            // Content headers (Content-Type...) belong to the body, the rest to the request
            if (!message.Headers.TryAddWithoutValidation(name, value)) message.Content?.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await _http.SendAsync(message, cancellation);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendObsAsync(ObsRequest request, CancellationToken cancellation)
    {
        var settings = _obs();
        await ObsClient.SendAsync(settings.Host, settings.Port, settings.Password, request, cancellation);
    }

    // ---- command lines (separate and public so they can be checked without starting anything) ---------------------

    public static ProcessStartInfo OpenCommand(string url, bool? windows = null, bool? mac = null)
    {
        if (windows ?? OperatingSystem.IsWindows()) return new ProcessStartInfo(url) { UseShellExecute = true };

        var psi = new ProcessStartInfo((mac ?? OperatingSystem.IsMacOS()) ? "/usr/bin/open" : "xdg-open") { UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add(url);
        return psi;
    }

    public static ProcessStartInfo LaunchCommand(string appOrPath, bool? windows = null, bool? mac = null)
    {
        if (windows ?? OperatingSystem.IsWindows()) return new ProcessStartInfo(appOrPath) { UseShellExecute = true };

        var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false, CreateNoWindow = true };
        // "Safari" is an app's name, "/Applications/Safari.app" or "~/notes.txt" is a path
        bool isPath = appOrPath.Contains('/') || appOrPath.StartsWith('~');
        if (!isPath) psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add(appOrPath.StartsWith("~/") ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), appOrPath[2..]) : appOrPath);
        return psi;
    }

    public static ProcessStartInfo ShellCommand(string command, bool? windows = null)
    {
        ProcessStartInfo psi;
        if (windows ?? OperatingSystem.IsWindows())
        {
            psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
        }
        else
        {
            psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);
        }
        return psi;
    }

    private void Run(ProcessStartInfo psi)
    {
        try
        {
            _start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
        {
            // A missing app or a command that cannot start must not stop the rest of the macro
        }
    }
}
