using System.Diagnostics;
using ReWASD.Core.Models;
using ReWASD.Engine.Actions;
using ReWASD.Engine.Foreground;
using ReWASD.Engine.Input;
using ReWASD.Engine.Scripting;

namespace ReWASD.Tests;

public class ScriptRunnerTests
{
    private sealed class RecordingEnvironment : IScriptEnvironment
    {
        public readonly List<string> Calls = new();
        public ForegroundApp? App = new("Safari", "com.apple.Safari", 7);
        public CancellationToken Token;

        public void KeyTap(string key) => Calls.Add("tap:" + key);
        public void KeyDown(string key) => Calls.Add("down:" + key);
        public void KeyUp(string key) => Calls.Add("up:" + key);
        public void MouseClick(string button) => Calls.Add("click:" + button);
        public void MouseDown(string button) => Calls.Add("mdown:" + button);
        public void MouseUp(string button) => Calls.Add("mup:" + button);
        public void Scroll(double notches) => Calls.Add("scroll:" + notches);
        public void MoveMouse(int dx, int dy) => Calls.Add($"move:{dx},{dy}");
        public void TypeText(string text) => Calls.Add("type:" + text);
        public void PadTap(string button) => Calls.Add("padtap:" + button);
        public void PadDown(string button) => Calls.Add("paddown:" + button);
        public void PadUp(string button) => Calls.Add("padup:" + button);
        public void Delay(int milliseconds)
        {
            Calls.Add("delay:" + milliseconds);
            Token.ThrowIfCancellationRequested();
        }
        public void RunShell(string command) => Calls.Add("shell:" + command);
        public void OpenUrl(string url) => Calls.Add("url:" + url);
        public void LaunchApp(string appOrPath) => Calls.Add("app:" + appOrPath);
        public void Webhook(WebhookRequest request) => Calls.Add($"hook:{request.Method} {request.Url} {request.Body}");
        public void Obs(ObsRequest request) => Calls.Add($"obs:{request.RequestType} {request.DataJson}");
        public void Notify(string message) => Calls.Add("notify:" + message);
        public ForegroundApp? CurrentApp() => App;
    }

    private static List<string> Run(string code, RecordingEnvironment? env = null, JintScriptRunner? runner = null)
    {
        env ??= new RecordingEnvironment();
        (runner ?? new JintScriptRunner()).Run(code, env, CancellationToken.None);
        return env.Calls;
    }

    [Fact]
    public void TheBasicFunctions_ReachTheEnvironment()
    {
        var calls = Run("""
            press('Space'); hold('W'); release('W');
            click(); click('right'); mouseDown('left'); mouseUp('left');
            scroll(3); scroll(); moveMouse(10, -5); type('hi'); delay(250);
            padPress('A'); padHold('LB'); padRelease('LB');
            """);

        Assert.Equal(new[]
        {
            "tap:Space", "down:W", "up:W", "click:left", "click:right", "mdown:left", "mup:left",
            "scroll:3", "scroll:1", "move:10,-5", "type:hi", "delay:250", "padtap:A", "paddown:LB", "padup:LB"
        }, calls);
    }

    [Fact]
    public void TheOutsideWorldFunctions_AreChecked_AndPassedOn()
    {
        var calls = Run("""
            shell('echo hi'); openURL('https://example.com'); openApp('Calculator');
            webhook('POST', 'https://x.test/h', { a: 1, b: 'two' });
            webhook('GET', 'https://x.test/ping');
            obs('SetCurrentProgramScene', { sceneName: 'Gaming' });
            obs('StartRecord');
            notify('done');
            """);

        Assert.Equal(new[]
        {
            "shell:echo hi", "url:https://example.com", "app:Calculator",
            "hook:POST https://x.test/h {\"a\":1,\"b\":\"two\"}", "hook:GET https://x.test/ping ",
            "obs:SetCurrentProgramScene {\"sceneName\":\"Gaming\"}", "obs:StartRecord ", "notify:done"
        }, calls);
    }

    [Fact]
    public void ABadWebhookAddress_IsAScriptError_ThatTheScriptCanCatch()
    {
        var calls = Run("try { webhook('POST', 'ftp://nope'); } catch (e) { notify('caught'); } notify('after');");

        Assert.Equal(new[] { "notify:caught", "notify:after" }, calls);
    }

    [Fact]
    public void TheAppObject_DescribesTheAppInFront()
    {
        var env = new RecordingEnvironment();
        var calls = Run("""
            notify(app.name + '|' + app.id);
            if (app.is('safari')) notify('yes'); else notify('no');
            if (app.is('chrome')) notify('chrome'); else notify('not chrome');
            """, env);
        Assert.Equal(new[] { "notify:Safari|com.apple.Safari", "notify:yes", "notify:not chrome" }, calls);

        env = new RecordingEnvironment { App = null };
        Assert.Equal(new[] { "notify:|", "notify:no" }, Run("notify(app.name + '|' + app.id); notify(app.is('x') ? 'yes' : 'no');", env));
    }

    [Fact]
    public void State_IsKeptBetweenRunsOfTheSameScript_ButNotSharedWithOthers()
    {
        var runner = new JintScriptRunner();
        const string counter = "state.n = (state.n || 0) + 1; notify('n=' + state.n);";

        Assert.Equal(new[] { "notify:n=1" }, Run(counter, runner: runner));
        Assert.Equal(new[] { "notify:n=2" }, Run(counter, runner: runner));
        Assert.Equal(new[] { "notify:n=3" }, Run(counter, runner: runner));
        Assert.Equal(new[] { "notify:other=undefined" }, Run("notify('other=' + state.n);", runner: runner));
        Assert.Equal(new[] { "notify:n=1" }, Run(counter, runner: new JintScriptRunner()));       // a fresh runner starts fresh
    }

    [Fact]
    public void State_IsKeptEvenWhenTheScriptFails()
    {
        var runner = new JintScriptRunner();
        const string code = "state.n = (state.n || 0) + 1; notify('n=' + state.n); if (state.n == 1) throw new Error('boom');";

        Assert.ThrowsAny<Exception>(() => Run(code, runner: runner));
        Assert.Equal(new[] { "notify:n=2" }, Run(code, runner: runner));
    }

    [Fact]
    public void AScriptError_SaysWhatWentWrong()
    {
        var ex = Assert.ThrowsAny<Exception>(() => Run("press('A'); nonsense();"));
        Assert.Contains("nonsense", ex.Message);
    }

    [Fact]
    public void Syntax_IsCheckedBeforeSaving()
    {
        Assert.True(JintScriptRunner.TryValidate("press('A');", out _));
        Assert.True(JintScriptRunner.TryValidate("// only a comment\nstate.x = 1;", out _));

        Assert.False(JintScriptRunner.TryValidate("press('A'", out var error));
        Assert.StartsWith("Script error", error);
        Assert.False(JintScriptRunner.TryValidate("   ", out error));
        Assert.Contains("empty", error);
    }

    [Fact]
    public void ARunawayLoop_IsStoppedByCancelling()
    {
        var runner = new JintScriptRunner();
        using var cts = new CancellationTokenSource();
        var env = new RecordingEnvironment();
        var task = Task.Run(() => runner.Run("while (true) { }", env, cts.Token));

        Thread.Sleep(150);
        cts.Cancel();

        var ex = Assert.Throws<AggregateException>(() => task.Wait(5000));
        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
    }

    [Fact]
    public void AScriptCannotSwallowTheCancel_WithTryCatch()
    {
        var runner = new JintScriptRunner();
        using var cts = new CancellationTokenSource();
        var env = new RecordingEnvironment { Token = cts.Token };
        var task = Task.Run(() => runner.Run("while (true) { try { while (true) { delay(5); } } catch (e) { } }", env, cts.Token));

        Thread.Sleep(150);
        cts.Cancel();

        Assert.True(Assert.Throws<AggregateException>(() => task.Wait(5000)).InnerException is OperationCanceledException);
    }

    [Fact]
    public void ACancelledDelay_EndsTheScript()
    {
        var runner = new JintScriptRunner();
        using var cts = new CancellationTokenSource();
        var env = new RecordingEnvironment { Token = cts.Token };
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => runner.Run("delay(10); notify('never');", env, cts.Token));
        Assert.DoesNotContain("notify:never", env.Calls);
    }
}

public class ScriptInMacroTests
{
    private sealed class FakeBackend : IInputBackend
    {
        private readonly object _gate = new();
        private readonly List<string> _events = new();
        public List<string> Events { get { lock (_gate) return _events.ToList(); } }
        private void Add(string e) { lock (_gate) _events.Add(e); }
        public void KeyDown(ushort virtualKey) => Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Add($"mu:{button}");
        public void MouseWheel(int delta) => Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Add($"move:{dx},{dy}");
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 4000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    [Fact]
    public void AScriptMacro_PressesKeysAndClicks_ThroughTheRealBackend()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend, scripts: new JintScriptRunner());

        runner.Update(new[] { MacroDefinition.FromScript("press('A'); click('right'); scroll(2); type('Hi');") });

        Assert.True(WaitFor(() => backend.Events.Count(e => e.StartsWith("ku:")) >= 4));
        var events = backend.Events;
        Assert.Equal(new[] { "kd:41", "ku:41", "md:Right", "mu:Right", "wheel:240" }, events.Take(5));
        Assert.Contains("kd:A0", events);        // Shift for the capital H
    }

    [Fact]
    public void KeysAScriptLeavesHeld_AreReleasedWhenItEnds()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend, scripts: new JintScriptRunner());

        runner.Update(new[] { MacroDefinition.FromScript("hold('W'); mouseDown('left');") });

        Assert.True(WaitFor(() => backend.Events.Contains("ku:57") && backend.Events.Contains("mu:Left")));
        Assert.Equal(new[] { "kd:57", "md:Left" }, backend.Events.Take(2));
    }

    [Fact]
    public void AWhileHeldScript_LoopsUntilTheButtonIsReleased()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend, scripts: new JintScriptRunner());
        var macro = MacroDefinition.FromScript("click(); delay(10);", MacroRepeatMode.WhileHeld);

        runner.Update(new[] { macro });
        Assert.True(WaitFor(() => backend.Events.Count(e => e == "md:Left") >= 3));
        runner.Update(Array.Empty<MacroDefinition>());
        Thread.Sleep(150);
        int after = backend.Events.Count;
        Thread.Sleep(150);

        Assert.Equal(after, backend.Events.Count);                                     // stopped
        Assert.Equal(backend.Events.Count(e => e == "md:Left"), backend.Events.Count(e => e == "mu:Left"));
    }

    [Fact]
    public void AScriptThatFails_IsReported_AndDoesNotStopLaterSteps()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend, scripts: new JintScriptRunner());
        var failures = new List<string>();
        runner.ActionFailed += m => { lock (failures) failures.Add(m); };
        var macro = new MacroDefinition();
        macro.Steps.Add(new MacroStep { Action = MacroActionType.Script, Value = "nonsense();" });
        macro.Steps.Add(new MacroStep { Action = MacroActionType.KeyTap, Value = "B" });

        runner.Update(new[] { macro });

        Assert.True(WaitFor(() => backend.Events.Contains("ku:42")));
        Assert.True(WaitFor(() => { lock (failures) return failures.Count == 1; }));
        Assert.StartsWith("Script failed:", failures[0]);
    }

    [Fact]
    public void Notify_ReachesTheApp()
    {
        using var runner = new MacroRunner(new FakeBackend(), scripts: new JintScriptRunner());
        var messages = new List<string>();
        runner.ScriptMessage += m => { lock (messages) messages.Add(m); };

        runner.Update(new[] { MacroDefinition.FromScript("notify('hello ' + app.name);") });

        Assert.True(WaitFor(() => { lock (messages) return messages.Count == 1; }));
        Assert.Equal("hello ", messages[0]);          // no foreground source was given
    }

    [Fact]
    public void AScriptMacro_IsRecognisedAndSurvivesSaving()
    {
        var macro = MacroDefinition.FromScript("a();\nb(); // c: d;");
        Assert.Equal("a();\nb(); // c: d;", macro.SingleScript);

        var target = new RemapTarget { Type = TargetType.Macro, Macro = macro };
        Assert.Equal("Script", target.DisplayText);

        var json = System.Text.Json.JsonSerializer.Serialize(target, new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        var back = System.Text.Json.JsonSerializer.Deserialize<RemapTarget>(json, new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;
        Assert.Equal(macro.SingleScript, back.Macro!.SingleScript);

        Assert.Null(new MacroDefinition().SingleScript);
        var plain = new MacroDefinition();
        plain.Steps.Add(new MacroStep { Action = MacroActionType.KeyTap, Value = "A" });
        Assert.Null(plain.SingleScript);
    }
}
