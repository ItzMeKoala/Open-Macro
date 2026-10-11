using System.Runtime.InteropServices;
using ReWASD.Core.Models;
using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;
using ReWASD.Engine.VirtualOutput;

namespace ReWASD.Engine;

/// <summary>Polls the physical pad, runs the mapping engine, and fans the result out to the virtual pad and keyboard/mouse.</summary>
public sealed class InputPipeline : IDisposable
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint ms);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint ms);

    private readonly MappingEngine _engine;
    private readonly IGamepadReader _reader;
    private readonly IVirtualGamepad _virtualPad;
    private readonly KeyboardMouseInjector _injector;

    private Thread? _thread;
    private volatile bool _running;

    private readonly MacroRunner? _macros;
    private readonly MacroPadState? _macroPad;
    private readonly IInputHook? _hook;

    private static readonly PhysicalState NoController = new(new HashSet<ControllerButton>(), 0, 0, 0, 0, 0, 0);

    public InputPipeline(MappingEngine engine, IGamepadReader reader, IVirtualGamepad virtualPad, KeyboardMouseInjector injector,
        MacroRunner? macros = null, MacroPadState? macroPad = null, IInputHook? hook = null)
    {
        _engine = engine;
        _reader = reader;
        _virtualPad = virtualPad;
        _injector = injector;
        _macros = macros;
        _macroPad = macroPad;
        _hook = hook;
    }

    /// <summary>False if the keyboard/mouse hook could not be installed, so keys and mouse buttons cannot be remapped.</summary>
    public bool KeyboardMouseHooked { get; private set; }

    public bool IsRunning => _running;

    /// <summary>Raised on the polling thread after every processed frame.</summary>
    public event Action<RemappedState>? FrameProcessed;

    public bool HasPhysicalController { get; private set; }

    /// <summary>The most recent physical pad reading (null when no pad is connected). Safe to read from any thread.</summary>
    public PhysicalState? LatestPhysical => _latestPhysical;
    private volatile PhysicalState? _latestPhysical;

    public void Start()
    {
        if (_running) return;
        _running = true;
        KeyboardMouseHooked = _hook?.Start() ?? false;
        if (OperatingSystem.IsWindows()) timeBeginPeriod(1); // default timer granularity is ~15ms, far too coarse for polling
        _thread = new Thread(Loop) { IsBackground = true, Name = "ReWASD input pipeline" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join();
        _thread = null;
        if (OperatingSystem.IsWindows()) timeEndPeriod(1);
        _hook?.Stop(); // stop swallowing input before anything else
        KeyboardMouseHooked = false;
        _injector.ReleaseAll();
        _macros?.CancelAll();
        _macroPad?.Clear();
        _engine.Reset();
    }

    private void Loop()
    {
        while (_running)
        {
            try
            {
                var physical = _reader.Read();
                HasPhysicalController = physical != null;
                _latestPhysical = physical;

                // Keyboard and mouse mappings must keep working without a controller, so every frame is processed
                physical ??= NoController;
                var (keys, mouse) = _hook?.Snapshot() ?? (Array.Empty<string>(), Array.Empty<string>());

                var state = _engine.ProcessInputs(
                    physical.Buttons, keys, mouse,
                    physical.LeftStickX, physical.LeftStickY,
                    physical.RightStickX, physical.RightStickY,
                    physical.LeftTrigger, physical.RightTrigger);

                _macroPad?.ApplyTo(state); // macro-driven pad buttons and axes join the frame
                _virtualPad.SendReport(state);
                _injector.Apply(state);
                _macros?.Update(state.ActiveMacros);
                FrameProcessed?.Invoke(state);
            }
            catch (Exception)
            {
                // A bad frame must not kill the polling thread
            }

            Thread.Sleep(1);
        }
    }

    public void Dispose() => Stop();
}
