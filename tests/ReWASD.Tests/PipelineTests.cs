using ReWASD.Core.Models;
using ReWASD.Engine;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.Tests;

/// <summary>The whole polling loop on fakes, so the same behaviour is checked on every operating system.</summary>
public class PipelineTests
{
    private sealed class FakeReader : IGamepadReader
    {
        public volatile PhysicalState? State;
        public PhysicalState? Read() => State;
    }

    private sealed class FakeBackend : IInputBackend
    {
        public readonly List<string> Events = new();
        private void Add(string e) { lock (Events) Events.Add(e); }
        public List<string> Copy() { lock (Events) return Events.ToList(); }
        public void KeyDown(ushort virtualKey) => Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Add($"mu:{button}");
        public void MouseWheel(int delta) => Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Add($"move:{dx},{dy}");
    }

    private sealed class FakeHook : IInputHook
    {
        public volatile string[] Keys = Array.Empty<string>();
        public bool IsInstalled { get; private set; }
        public bool IncludeInjected { get; set; }
        public int Starts, Stops;
        public event Action? PanicRequested { add { } remove { } }
        public event Action<string, bool>? KeyEvent { add { } remove { } }
        public event Action<string, bool>? MouseEvent { add { } remove { } }
        public bool Start() { Starts++; return IsInstalled = true; }
        public void Stop() { Stops++; IsInstalled = false; }
        public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot() => (Keys, Array.Empty<string>());
        public void Dispose() => Stop();
    }

    private static PhysicalState Pad(params ControllerButton[] pressed) =>
        new(new HashSet<ControllerButton>(pressed), 0, 0, 0, 0, 0, 0);

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    private static Profile ProfileWith(Action<ShiftLayer> configure)
    {
        var profile = Profile.CreateDefault();
        configure(profile.ShiftLayers[0]);
        return profile;
    }

    [Fact]
    public void ControllerButton_RemappedToAKey_PressesAndReleasesIt()
    {
        var profile = ProfileWith(layer => layer.Mappings[ControllerButton.A].Target =
            new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "Space" });
        var engine = new MappingEngine(profile) { IsActive = true };
        var reader = new FakeReader();
        var backend = new FakeBackend();
        using var pipeline = new InputPipeline(engine, reader, new NullVirtualGamepad(), new KeyboardMouseInjector(backend));

        pipeline.Start();
        reader.State = Pad(ControllerButton.A);
        Assert.True(WaitFor(() => backend.Copy().Contains("kd:20")), "Space was never pressed");
        Assert.True(pipeline.HasPhysicalController);

        reader.State = Pad();
        Assert.True(WaitFor(() => backend.Copy().Contains("ku:20")), "Space was never released");
        pipeline.Stop();
    }

    [Fact]
    public void Stopping_ReleasesAnythingStillHeld()
    {
        var profile = ProfileWith(layer => layer.Mappings[ControllerButton.A].Target =
            new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "W" });
        var engine = new MappingEngine(profile) { IsActive = true };
        var reader = new FakeReader { State = Pad(ControllerButton.A) };
        var backend = new FakeBackend();
        using var pipeline = new InputPipeline(engine, reader, new NullVirtualGamepad(), new KeyboardMouseInjector(backend));

        pipeline.Start();
        Assert.True(WaitFor(() => backend.Copy().Contains("kd:57")));
        pipeline.Stop();

        Assert.Contains("ku:57", backend.Copy());
        Assert.False(pipeline.IsRunning);
    }

    [Fact]
    public void KeyboardSource_IsRemappedWithoutAnyController()
    {
        var profile = ProfileWith(layer => layer.KeyboardMappings["Q"] = new ButtonMapping
        {
            Target = new RemapTarget { Type = TargetType.KeyboardKey, KeyboardKey = "E" }
        });
        var engine = new MappingEngine(profile) { IsActive = true };
        var hook = new FakeHook();
        var backend = new FakeBackend();
        using var pipeline = new InputPipeline(engine, new FakeReader(), new NullVirtualGamepad(), new KeyboardMouseInjector(backend), hook: hook);

        pipeline.Start();
        Assert.True(pipeline.KeyboardMouseHooked);
        hook.Keys = new[] { "Q" };

        Assert.True(WaitFor(() => backend.Copy().Contains("kd:45")), "E was never pressed");
        Assert.False(pipeline.HasPhysicalController);

        pipeline.Stop();
        Assert.Equal(1, hook.Stops);
        Assert.False(pipeline.KeyboardMouseHooked);
    }

    [Fact]
    public void FramesAreReported_WithNoVirtualPadAndNoHook()
    {
        var engine = new MappingEngine(Profile.CreateDefault()) { IsActive = true };
        using var pipeline = new InputPipeline(engine, new FakeReader(), new NullVirtualGamepad(), new KeyboardMouseInjector(new FakeBackend()));
        int frames = 0;
        pipeline.FrameProcessed += _ => Interlocked.Increment(ref frames);

        pipeline.Start();

        Assert.True(WaitFor(() => frames > 5));
        pipeline.Stop();
    }
}
