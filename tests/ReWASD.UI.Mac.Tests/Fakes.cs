using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Platform;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.UI.Mac.Tests;

/// <summary>Stand-ins for the operating system so the interface can be built and driven on any machine.</summary>
internal static class Fakes
{
    public sealed class Pad : IPhysicalController
    {
        public PhysicalState? State;
        public int ExcludedUserIndex { get; set; } = -1;
        public int ActiveUserIndex => State == null ? -1 : 0;
        public PhysicalState? Read() => State;
        public IReadOnlyList<int> ConnectedSlots() => State == null ? Array.Empty<int>() : new[] { 0 };
        public string DescribeSlot(int slot) => "Test Controller";
    }

    public sealed class Backend : IInputBackend
    {
        public readonly List<string> Events = new();
        public void KeyDown(ushort virtualKey) => Events.Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Events.Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Events.Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Events.Add($"mu:{button}");
        public void MouseWheel(int delta) => Events.Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Events.Add($"move:{dx},{dy}");
    }

    public sealed class Hook : IInputHook
    {
        public bool IsInstalled { get; private set; }
        public bool IncludeInjected { get; set; }
        public event Action? PanicRequested;
        public event Action<string, bool>? KeyEvent { add { } remove { } }
        public event Action<string, bool>? MouseEvent { add { } remove { } }
        public bool Start() => IsInstalled = true;
        public void Stop() => IsInstalled = false;
        public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot() => (Array.Empty<string>(), Array.Empty<string>());
        public void Dispose() => Stop();
        public void RaisePanic() => PanicRequested?.Invoke();
    }

    public sealed class Ui : IPlatformUi
    {
        public void Post(Action action) => action();
        public IDisposable StartTimer(TimeSpan interval, Action tick) => new Nop();
        public System.Collections.IEnumerable? CreateGroupedView(System.Collections.IEnumerable source, string groupProperty) => null;
        private sealed class Nop : IDisposable { public void Dispose() { } }
    }

    public static PlatformBackends Platform(PlatformInfo info, Pad? pad = null, Backend? backend = null, Hook? hook = null) => new()
    {
        Info = info,
        Reader = pad ?? new Pad(),
        InputBackend = backend ?? new Backend(),
        Rumble = new NullRumbleOutput(),
        VirtualPad = new NullVirtualGamepad(),
        CreateHook = (_, _) => hook ?? new Hook(),
        MissingPermissions = () => null
    };

    public static string TempProfiles() => Path.Combine(Path.GetTempPath(), "rewasd-tests-" + Guid.NewGuid().ToString("N"));
}
