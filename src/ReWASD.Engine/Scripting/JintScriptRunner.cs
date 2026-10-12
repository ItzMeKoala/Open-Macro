using Jint;
using JintEngine = Jint.Engine;
using ReWASD.Core.Models;
using ReWASD.Engine.Foreground;

namespace ReWASD.Engine.Scripting;

/// <summary>
/// Runs JavaScript with Jint, a JavaScript interpreter written in C# (so it needs nothing installed and behaves the same on
/// Windows and macOS). Scripts get press(), hold(), release(), click(), type(), delay(), shell(), openURL(), webhook(), obs() and
/// more, an <c>app</c> object for the app in front, and a <c>state</c> object whose contents are kept between runs.
/// </summary>
public sealed class JintScriptRunner : IScriptRunner
{
    private const string Prelude = """
        function press(k) { __tap(String(k)); }
        function hold(k) { __down(String(k)); }
        function release(k) { __up(String(k)); }
        function click(b) { __click(b === undefined ? 'left' : String(b)); }
        function mouseDown(b) { __mouseDown(b === undefined ? 'left' : String(b)); }
        function mouseUp(b) { __mouseUp(b === undefined ? 'left' : String(b)); }
        function scroll(n) { __scroll(n === undefined ? 1 : Number(n)); }
        function moveMouse(dx, dy) { __move(Math.round(Number(dx) || 0), Math.round(Number(dy) || 0)); }
        function type(t) { __type(String(t)); }
        function delay(ms) { __delay(Math.max(0, Math.round(Number(ms) || 0))); }
        function shell(c) { __shell(String(c)); }
        function openURL(u) { __url(String(u)); }
        function openApp(a) { __app(String(a)); }
        function webhook(method, url, body) {
            __webhook(String(method), String(url), body === undefined || body === null ? '' : (typeof body === 'string' ? body : JSON.stringify(body)));
        }
        function obs(request, data) {
            __obs(String(request), data === undefined || data === null ? '' : (typeof data === 'string' ? data : JSON.stringify(data)));
        }
        function padPress(b) { __padTap(String(b)); }
        function padHold(b) { __padDown(String(b)); }
        function padRelease(b) { __padUp(String(b)); }
        function notify(m) { __notify(String(m)); }
        const app = {
            get name() { return __appName(); },
            get id() { return __appId(); },
            is(x) { return __appIs(String(x)); }
        };
        """;

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _state = new();

    /// <summary>Checks that a script can be read, without running it. Returns false and a message with the line if it cannot.</summary>
    public static bool TryValidate(string code, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            error = "The script is empty";
            return false;
        }

        try
        {
            JintEngine.PrepareScript(code);
            return true;
        }
        catch (Exception ex) when (ex is Jint.ScriptPreparationException or Jint.Runtime.JavaScriptException or Acornima.ParseErrorException)
        {
            error = "Script error: " + ex.Message;
            return false;
        }
    }

    public void Run(string code, IScriptEnvironment env, CancellationToken cancellation)
    {
        string key = code;
        string saved;
        lock (_gate) saved = _state.TryGetValue(key, out var s) ? s : "{}";

        var engine = new JintEngine(options =>
        {
            options.CancellationToken(cancellation);
            options.LimitMemory(32_000_000);
            options.LimitRecursion(256);
            options.Strict(false);

            // A failing call (a bad address, say) can be caught by the script with try/catch; cancelling can never be caught
            options.CatchClrExceptions(ex => ex is not OperationCanceledException);
        });

        Bind(engine, env);
        engine.Execute(Prelude);
        engine.SetValue("__savedState", saved);
        engine.Execute("var state = JSON.parse(__savedState);");

        try
        {
            engine.Execute(code);
        }
        catch (Jint.Runtime.ExecutionCanceledException)
        {
            throw new OperationCanceledException(cancellation);
        }
        finally
        {
            // Keep whatever the script stored, even when it ended with an error or was cancelled
            try
            {
                string now = engine.Evaluate("JSON.stringify(state)").AsString();
                lock (_gate) _state[key] = now;
            }
            catch (Exception ex) when (ex is Jint.Runtime.JavaScriptException or InvalidOperationException)
            {
                // A script that made its state something that cannot be saved just starts fresh next time
            }
        }
    }

    private static void Bind(JintEngine engine, IScriptEnvironment env)
    {
        engine.SetValue("__tap", new Action<string>(env.KeyTap));
        engine.SetValue("__down", new Action<string>(env.KeyDown));
        engine.SetValue("__up", new Action<string>(env.KeyUp));
        engine.SetValue("__click", new Action<string>(env.MouseClick));
        engine.SetValue("__mouseDown", new Action<string>(env.MouseDown));
        engine.SetValue("__mouseUp", new Action<string>(env.MouseUp));
        engine.SetValue("__scroll", new Action<double>(env.Scroll));
        engine.SetValue("__move", new Action<int, int>(env.MoveMouse));
        engine.SetValue("__type", new Action<string>(env.TypeText));
        engine.SetValue("__padTap", new Action<string>(env.PadTap));
        engine.SetValue("__padDown", new Action<string>(env.PadDown));
        engine.SetValue("__padUp", new Action<string>(env.PadUp));
        engine.SetValue("__delay", new Action<int>(env.Delay));
        engine.SetValue("__shell", new Action<string>(env.RunShell));
        engine.SetValue("__url", new Action<string>(env.OpenUrl));
        engine.SetValue("__app", new Action<string>(env.LaunchApp));
        engine.SetValue("__notify", new Action<string>(env.Notify));

        engine.SetValue("__webhook", new Action<string, string, string>((method, url, body) =>
        {
            string text = $"{method} {url} {body}".Trim();
            if (!WebhookRequest.TryParse(text, out var request, out var error)) throw new ArgumentException(error);
            env.Webhook(request);
        }));

        engine.SetValue("__obs", new Action<string, string>((type, data) =>
        {
            if (!ObsRequest.TryParse((type + " " + data).Trim(), out var request, out var error)) throw new ArgumentException(error);
            env.Obs(request);
        }));

        engine.SetValue("__appName", new Func<string>(() => env.CurrentApp()?.Name ?? string.Empty));
        engine.SetValue("__appId", new Func<string>(() => env.CurrentApp()?.Id ?? string.Empty));
        engine.SetValue("__appIs", new Func<string, bool>(pattern => AppMatcher.Matches(pattern, env.CurrentApp())));
    }
}
