using ReWASD.Core.Models;

namespace ReWASD.Engine.Actions;

/// <summary>The things a macro can do outside the keyboard and mouse: open things, run commands, call the network.</summary>
public interface ISystemActions
{
    void OpenUrl(string url);
    void LaunchApp(string appOrPath);
    void RunShell(string command);
    Task SendWebhookAsync(WebhookRequest request, CancellationToken cancellation);
    Task SendObsAsync(ObsRequest request, CancellationToken cancellation);
}

/// <summary>Where OBS Studio's WebSocket server is. The password is kept on this computer only, never in a profile.</summary>
public sealed class ObsSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4455;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Does nothing; used where running real commands would be wrong (tests, tools).</summary>
public sealed class NullSystemActions : ISystemActions
{
    public void OpenUrl(string url) { }
    public void LaunchApp(string appOrPath) { }
    public void RunShell(string command) { }
    public Task SendWebhookAsync(WebhookRequest request, CancellationToken cancellation) => Task.CompletedTask;
    public Task SendObsAsync(ObsRequest request, CancellationToken cancellation) => Task.CompletedTask;
}
