using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ReWASD.Core.Models;
using ReWASD.Engine.Actions;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform.MacOS;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class MacroTextStepTests
{
    private static MacroDefinition Parse(string script)
    {
        Assert.True(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out var macro, out var error), error);
        return macro;
    }

    [Fact]
    public void TextSteps_KeepColonsInTheirValue()
    {
        var macro = Parse("OpenUrl:https://example.com:8080/a?b=c; Shell:echo a:b; TypeText:Time: 10:30");

        Assert.Equal(new[] { "https://example.com:8080/a?b=c", "echo a:b", "Time: 10:30" }, macro.Steps.Select(s => s.Value));
        Assert.Equal(new[] { MacroActionType.OpenUrl, MacroActionType.Shell, MacroActionType.TypeText }, macro.Steps.Select(s => s.Action));
    }

    [Fact]
    public void ASemicolonInText_IsWrittenWithABackslash_AndSurvivesRoundTrips()
    {
        var macro = Parse(@"Shell:echo one\; echo two; KeyTap:A");

        Assert.Equal(2, macro.Steps.Count);
        Assert.Equal("echo one; echo two", macro.Steps[0].Value);

        var again = Parse(macro.ToScript());
        Assert.Equal(macro.Steps.Select(s => (s.Action, s.Value)), again.Steps.Select(s => (s.Action, s.Value)));
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\")]
    [InlineData(@"a\;b")]
    [InlineData(@"x\")]
    [InlineData("plain")]
    public void AnyText_SurvivesTheRoundTrip(string text)
    {
        var macro = new MacroDefinition();
        macro.Steps.Add(new MacroStep { Action = MacroActionType.LaunchApp, Value = text });
        macro.Steps.Add(new MacroStep { Action = MacroActionType.KeyTap, Value = "A" });

        var again = Parse(macro.ToScript());

        Assert.Equal(2, again.Steps.Count);
        Assert.Equal(text, again.Steps[0].Value);
    }

    [Theory]
    [InlineData("OpenUrl:", "must look like")]
    [InlineData("OpenUrl:not a url", "not a web address")]
    [InlineData("OpenUrl:file:///etc/passwd", "not a web address")]
    [InlineData("OpenUrl:javascript:alert(1)", "not a web address")]
    [InlineData("Webhook:POST ftp://x", "http or https")]
    [InlineData("Webhook:POST https://x -H nonsense", "-H")]
    [InlineData("Obs:Scene", "scene's name")]
    [InlineData("Obs:SetX {not json}", "JSON object")]
    [InlineData("Shell:", "must look like")]
    public void BadTextSteps_AreRefusedWithAnExplanation(string script, string expected)
    {
        Assert.False(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out _, out var error));
        Assert.Contains(expected, error);
    }

    [Fact]
    public void ExistingScripts_ParseExactlyAsBefore()
    {
        var macro = Parse("KeyDown:W; Delay:50; MouseMove:up:20; PadAxis:LeftStickX:50; KeyUp:W");
        Assert.Equal(5, macro.Steps.Count);
        Assert.Equal("KeyDown:W; Delay:50; MouseMove:up:20; PadAxis:LeftStickX:50; KeyUp:W", macro.ToScript());
    }

    [Fact]
    public void Webhook_ParsesMethodAddressHeadersAndBody()
    {
        Assert.True(WebhookRequest.TryParse("post https://x.test/hook -H \"Authorization=Bearer abc\" -H X-A=1 {\"a\": 1, \"b\": [2]}", out var r, out _));
        Assert.Equal("POST", r.Method);
        Assert.Equal("https://x.test/hook", r.Url);
        Assert.Equal(new[] { "Authorization", "X-A" }, r.Headers.Select(h => h.Key));
        Assert.Equal("Bearer abc", r.Headers[0].Value);
        Assert.Equal("{\"a\": 1, \"b\": [2]}", r.Body);

        Assert.True(WebhookRequest.TryParse("https://x.test/ping", out var bare, out _));
        Assert.Equal("GET", bare.Method);
        Assert.Equal("", bare.Body);
    }

    [Fact]
    public void ObsRequests_ParseShortFormsAndRawRequests()
    {
        Assert.True(ObsRequest.TryParse("Scene My Game \"HD\"", out var scene, out _));
        Assert.Equal("SetCurrentProgramScene", scene.RequestType);
        Assert.Equal("My Game \"HD\"", JsonDocument.Parse(scene.DataJson).RootElement.GetProperty("sceneName").GetString());

        Assert.True(ObsRequest.TryParse("mute Mic", out var mute, out _));
        Assert.Equal("SetInputMute", mute.RequestType);
        Assert.True(JsonDocument.Parse(mute.DataJson).RootElement.GetProperty("inputMuted").GetBoolean());

        Assert.True(ObsRequest.TryParse("record", out var record, out _));
        Assert.Equal("ToggleRecord", record.RequestType);

        Assert.True(ObsRequest.TryParse("StartStream", out var raw, out _));
        Assert.Equal(("StartStream", ""), (raw.RequestType, raw.DataJson));

        Assert.True(ObsRequest.TryParse("SetCurrentProgramScene {\"sceneName\":\"A\"}", out var withData, out _));
        Assert.Equal("{\"sceneName\":\"A\"}", withData.DataJson);
    }
}

public class KeyTypingTests
{
    [Theory]
    [InlineData('a', 'A', false)]
    [InlineData('Z', 'Z', true)]
    [InlineData('5', '5', false)]
    [InlineData('%', '5', true)]
    [InlineData(')', '0', true)]
    [InlineData(' ', 0x20, false)]
    [InlineData(':', 0xBA, true)]
    [InlineData(';', 0xBA, false)]
    [InlineData('?', 0xBF, true)]
    [InlineData('\'', 0xDE, false)]
    [InlineData('"', 0xDE, true)]
    [InlineData('\\', 0xDC, false)]
    public void CharactersMapToUsKeys(char c, int vk, bool shift)
    {
        Assert.True(KeyMap.TryGetTypedKey(c, out ushort key, out bool needsShift));
        Assert.Equal((ushort)vk, key);
        Assert.Equal(shift, needsShift);
    }

    [Fact]
    public void CharactersWithoutAKey_AreNotTyped()
    {
        Assert.False(KeyMap.TryGetTypedKey('é', out _, out _));
        Assert.False(KeyMap.TryGetTypedKey('€', out _, out _));
    }

    [Fact]
    public void EveryTypableCharacter_HasAMacKeyCode()
    {
        foreach (char c in "abcxyzABCXYZ0123456789 !@#$%^&*()-_=+[]{}\\|;:'\",<.>/?`~\n\t")
        {
            Assert.True(KeyMap.TryGetTypedKey(c, out ushort vk, out _), $"'{c}' has no key");
            Assert.True(MacKeyCodes.TryGetMacKeyCode(vk, out _), $"'{c}' (0x{vk:X}) has no Mac key code");
        }
    }
}

public class SystemActionsTests
{
    [Fact]
    public void TheCommandLines_AreRightForEachSystem()
    {
        var shellWin = SystemActions.ShellCommand("echo hi & dir", windows: true);
        Assert.Equal("cmd.exe", shellWin.FileName);
        Assert.Equal(new[] { "/c", "echo hi & dir" }, shellWin.ArgumentList);

        var shellMac = SystemActions.ShellCommand("echo 'hi there'", windows: false);
        Assert.Equal("/bin/sh", shellMac.FileName);
        Assert.Equal(new[] { "-c", "echo 'hi there'" }, shellMac.ArgumentList);

        var openMac = SystemActions.OpenCommand("https://x.test/?a=1&b=2", windows: false, mac: true);
        Assert.Equal("/usr/bin/open", openMac.FileName);
        Assert.Equal(new[] { "https://x.test/?a=1&b=2" }, openMac.ArgumentList);

        var openWin = SystemActions.OpenCommand("https://x.test/", windows: true);
        Assert.True(openWin.UseShellExecute);

        Assert.Equal(new[] { "-a", "Safari" }, SystemActions.LaunchCommand("Safari", windows: false).ArgumentList);
        Assert.Equal(new[] { "/Applications/Safari.app" }, SystemActions.LaunchCommand("/Applications/Safari.app", windows: false).ArgumentList);
        Assert.Equal("notepad.exe", SystemActions.LaunchCommand("notepad.exe", windows: true).FileName);
    }

    [Fact]
    public void TheActions_HandOverTheRightCommands_AndRefuseDangerousAddresses()
    {
        var started = new List<ProcessStartInfo>();
        var actions = new SystemActions(start: psi => { started.Add(psi); return true; });

        actions.OpenUrl("https://example.com");
        actions.OpenUrl("file:///etc/passwd");
        actions.OpenUrl("javascript:alert(1)");
        actions.OpenUrl("not a url");
        actions.RunShell("   ");
        actions.LaunchApp("");
        actions.RunShell("echo ok");

        Assert.Equal(2, started.Count);
    }

    [Fact]
    public void ACommandThatCannotStart_DoesNotThrow()
    {
        var actions = new SystemActions(start: _ => throw new System.ComponentModel.Win32Exception("no such file"));
        actions.RunShell("nope");
        actions.LaunchApp("NoSuchApp");
    }

    // ---- real network calls against a local server ----------------------------------------------------------------

    private static (HttpListener Listener, string Url) StartListener()
    {
        for (int attempt = 0; ; attempt++)
        {
            int port = Random.Shared.Next(20000, 40000);
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, $"http://127.0.0.1:{port}/");
            }
            catch (HttpListenerException) when (attempt < 20)
            {
            }
        }
    }

    [Fact]
    public async Task AWebhook_SendsMethodHeadersAndBody()
    {
        var (listener, url) = StartListener();
        using var keep = listener;
        var seen = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            string body = await new StreamReader(ctx.Request.InputStream).ReadToEndAsync();
            var result = (ctx.Request.HttpMethod, ctx.Request.Url!.AbsolutePath, ctx.Request.Headers["X-Test"], ctx.Request.Headers["Authorization"], ctx.Request.ContentType, body);
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
            return result;
        });

        Assert.True(WebhookRequest.TryParse($"PUT {url}hook -H X-Test=yes -H \"Authorization=Bearer t\" {{\"on\":true}}", out var request, out _));
        await new SystemActions().SendWebhookAsync(request, CancellationToken.None);

        var (method, path, header, auth, type, body) = await seen;
        Assert.Equal("PUT", method);
        Assert.Equal("/hook", path);
        Assert.Equal("yes", header);
        Assert.Equal("Bearer t", auth);
        Assert.StartsWith("application/json", type);
        Assert.Equal("{\"on\":true}", body);
    }

    [Fact]
    public async Task AWebhookThatTheServerRefuses_Throws()
    {
        var (listener, url) = StartListener();
        using var keep = listener;
        _ = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        });

        Assert.True(WebhookRequest.TryParse($"GET {url}", out var request, out _));
        await Assert.ThrowsAsync<HttpRequestException>(() => new SystemActions().SendWebhookAsync(request, CancellationToken.None));
    }

    // A tiny OBS: says hello (with a password challenge when asked), checks the sign-in, answers one request
    private static async Task<List<string>> RunFakeObs(HttpListener listener, string? password, bool succeed = true)
    {
        var received = new List<string>();
        var ctx = await listener.GetContextAsync();
        var ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;

        async Task Send(object o) => await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(o), WebSocketMessageType.Text, true, CancellationToken.None);
        async Task<JsonElement> Receive()
        {
            var buf = new byte[8192];
            var r = await ws.ReceiveAsync(buf, CancellationToken.None);
            return JsonDocument.Parse(buf.AsMemory(0, r.Count).ToArray()).RootElement.Clone();
        }

        const string salt = "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=";
        const string challenge = "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY=";
        await Send(password == null
            ? new { op = 0, d = new { obsWebSocketVersion = "5.0.0", rpcVersion = 1 } }
            : new { op = 0, d = new { obsWebSocketVersion = "5.0.0", rpcVersion = 1, authentication = new { challenge, salt } } });

        var identify = await Receive();
        string? auth = identify.GetProperty("d").TryGetProperty("authentication", out var a) ? a.GetString() : null;
        received.Add("auth:" + (auth == (password == null ? null : ObsClient.Authentication(password, salt, challenge)) ? "ok" : "wrong"));
        await Send(new { op = 2, d = new { negotiatedRpcVersion = 1 } });

        var request = await Receive();
        var d = request.GetProperty("d");
        received.Add(d.GetProperty("requestType").GetString()!);
        received.Add(d.GetProperty("requestData").GetRawText());
        await Send(new { op = 5, d = new { eventType = "StudioModeStateChanged" } });   // chatter to skip
        await Send(new { op = 7, d = new { requestType = d.GetProperty("requestType").GetString(), requestId = d.GetProperty("requestId").GetString(),
            requestStatus = new { result = succeed, code = succeed ? 100 : 600, comment = succeed ? null : "No scene" } } });

        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch (WebSocketException) { }
        return received;
    }

    private static (HttpListener Listener, int Port) StartObsListener()
    {
        var (listener, url) = StartListener();
        return (listener, new Uri(url).Port);
    }

    [Fact]
    public async Task Obs_SendsARequest_WithoutAPassword()
    {
        var (listener, port) = StartObsListener();
        using var keep = listener;
        var server = RunFakeObs(listener, password: null);

        Assert.True(ObsRequest.TryParse("Scene Gaming", out var request, out _));
        await ObsClient.SendAsync("127.0.0.1", port, "", request, CancellationToken.None);

        var got = await server;
        Assert.Equal(new[] { "auth:ok", "SetCurrentProgramScene", "{\"sceneName\":\"Gaming\"}" }, got);
    }

    [Fact]
    public async Task Obs_SignsInWithThePassword()
    {
        var (listener, port) = StartObsListener();
        using var keep = listener;
        var server = RunFakeObs(listener, password: "secret");

        Assert.True(ObsRequest.TryParse("StartRecord", out var request, out _));
        await ObsClient.SendAsync("127.0.0.1", port, "secret", request, CancellationToken.None);

        Assert.Equal(new[] { "auth:ok", "StartRecord", "{}" }, await server);
    }

    [Fact]
    public async Task Obs_AsksForAPassword_WhenNoneIsSet_AndReportsRefusals()
    {
        var (listener, port) = StartObsListener();
        using var keep = listener;
        _ = RunFakeObs(listener, password: "secret");
        Assert.True(ObsRequest.TryParse("StartRecord", out var request, out _));
        var needsPassword = await Assert.ThrowsAsync<InvalidOperationException>(() => ObsClient.SendAsync("127.0.0.1", port, "", request, CancellationToken.None));
        Assert.Contains("password", needsPassword.Message);

        var (listener2, port2) = StartObsListener();
        using var keep2 = listener2;
        var server = RunFakeObs(listener2, password: null, succeed: false);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ObsClient.SendAsync("127.0.0.1", port2, "", request, CancellationToken.None));
        Assert.Contains("No scene", refused.Message);
        await server;
    }

    [Fact]
    public async Task Obs_NotRunning_FailsInsteadOfHanging()
    {
        Assert.True(ObsRequest.TryParse("StartRecord", out var request, out _));
        var sw = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<Exception>(() => ObsClient.SendAsync("127.0.0.1", 1, "", request, CancellationToken.None));

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
    }
}

public class MacroSystemStepTests
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

    private sealed class FakeSystem : ISystemActions
    {
        public readonly List<string> Calls = new();
        public bool FailWebhook;
        public void OpenUrl(string url) { lock (Calls) Calls.Add("url:" + url); }
        public void LaunchApp(string appOrPath) { lock (Calls) Calls.Add("app:" + appOrPath); }
        public void RunShell(string command) { lock (Calls) Calls.Add("shell:" + command); }
        public Task SendWebhookAsync(WebhookRequest request, CancellationToken cancellation)
        {
            lock (Calls) Calls.Add($"hook:{request.Method} {request.Url} {request.Body}");
            return FailWebhook ? Task.FromException(new HttpRequestException("boom")) : Task.CompletedTask;
        }
        public Task SendObsAsync(ObsRequest request, CancellationToken cancellation)
        {
            lock (Calls) Calls.Add($"obs:{request.RequestType} {request.DataJson}");
            return Task.CompletedTask;
        }
        public List<string> Snapshot() { lock (Calls) return Calls.ToList(); }
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    private static MacroDefinition Macro(string script)
    {
        Assert.True(MacroDefinition.TryParseScript(script, MacroRepeatMode.Once, out var macro, out var error), error);
        return macro;
    }

    [Fact]
    public void EachStep_ReachesTheSystemActions_InOrder()
    {
        var system = new FakeSystem();
        using var runner = new MacroRunner(new FakeBackend(), system: system);

        runner.Update(new[] { Macro("OpenUrl:https://a.test; LaunchApp:Safari; Shell:echo hi; Webhook:POST https://h.test/x {\"a\":1}; Obs:Scene Gaming") });

        Assert.True(WaitFor(() => system.Snapshot().Count == 5));
        Assert.Equal(new[]
        {
            "url:https://a.test", "app:Safari", "shell:echo hi", "hook:POST https://h.test/x {\"a\":1}", "obs:SetCurrentProgramScene {\"sceneName\":\"Gaming\"}"
        }, system.Snapshot());
    }

    [Fact]
    public void AFailingWebhook_IsReported_AndTheMacroCarriesOn()
    {
        var system = new FakeSystem { FailWebhook = true };
        using var runner = new MacroRunner(new FakeBackend(), system: system);
        var failures = new List<string>();
        runner.ActionFailed += m => { lock (failures) failures.Add(m); };

        runner.Update(new[] { Macro("Webhook:GET https://h.test/; Shell:echo after") });

        Assert.True(WaitFor(() => system.Snapshot().Contains("shell:echo after")));
        Assert.True(WaitFor(() => { lock (failures) return failures.Count == 1; }));
        Assert.Contains("Webhook failed: boom", failures[0]);
    }

    [Fact]
    public void WithoutSystemActions_TheseStepsDoNothing()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend);

        runner.Update(new[] { Macro("Shell:echo hi; OpenUrl:https://a.test; KeyTap:A") });

        Assert.True(WaitFor(() => backend.Events.Contains("ku:41")));
    }

    [Fact]
    public void TypeText_PressesKeysWithShiftWhereNeeded()
    {
        var backend = new FakeBackend();
        using var runner = new MacroRunner(backend, system: new FakeSystem());

        runner.Update(new[] { Macro("TypeText:Hi!") });

        Assert.True(WaitFor(() => backend.Events.Count >= 10));
        Assert.Equal(new[]
        {
            "kd:A0", "kd:48", "ku:48", "ku:A0",     // H
            "kd:49", "ku:49",                        // i
            "kd:A0", "kd:31", "ku:31", "ku:A0"       // !
        }, backend.Events.Take(10));
    }
}

public class MacroStartedEventTests
{
    private sealed class NullBackend : IInputBackend
    {
        public void KeyDown(ushort virtualKey) { }
        public void KeyUp(ushort virtualKey) { }
        public void MouseDown(MouseButtonKind button) { }
        public void MouseUp(MouseButtonKind button) { }
        public void MouseWheel(int delta) { }
        public void MouseMove(int dx, int dy) { }
    }

    [Fact]
    public void EachStartOfAMacro_IsReportedOnce()
    {
        using var runner = new MacroRunner(new NullBackend());
        int started = 0;
        runner.MacroStarted += () => Interlocked.Increment(ref started);
        Assert.True(MacroDefinition.TryParseScript("KeyTap:A", MacroRepeatMode.Once, out var macro, out _));

        runner.Update(new[] { macro });
        runner.Update(new[] { macro });          // still held: not another start
        runner.Update(Array.Empty<MacroDefinition>());
        runner.Update(new[] { macro });

        Assert.Equal(2, started);
    }
}
