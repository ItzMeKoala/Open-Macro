using ReWASD.Core.Models;
using ReWASD.Engine.Foreground;

namespace ReWASD.Engine.Scripting;

/// <summary>What a script can do. The macro runner supplies one per run and takes care of releasing anything the script left held.</summary>
public interface IScriptEnvironment
{
    void KeyTap(string key);
    void KeyDown(string key);
    void KeyUp(string key);
    void MouseClick(string button);
    void MouseDown(string button);
    void MouseUp(string button);
    void Scroll(double notches);
    void MoveMouse(int dx, int dy);
    void TypeText(string text);
    void PadTap(string button);
    void PadDown(string button);
    void PadUp(string button);

    /// <summary>Waits; ends early (by throwing) if the script is cancelled.</summary>
    void Delay(int milliseconds);

    void RunShell(string command);
    void OpenUrl(string url);
    void LaunchApp(string appOrPath);
    void Webhook(WebhookRequest request);
    void Obs(ObsRequest request);
    void Notify(string message);

    ForegroundApp? CurrentApp();
}

/// <summary>Runs a script. Blocking: returns when the script ends, fails, or is cancelled.</summary>
public interface IScriptRunner
{
    void Run(string code, IScriptEnvironment environment, CancellationToken cancellation);
}
