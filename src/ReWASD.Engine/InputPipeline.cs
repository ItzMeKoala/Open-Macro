using System.Runtime.InteropServices;
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
    private readonly VirtualGamepadService _virtualPad;
    private readonly KeyboardMouseInjector _injector;

    private Thread? _thread;
    private volatile bool _running;

    private readonly MacroRunner? _macros;

    public InputPipeline(MappingEngine engine, IGamepadReader reader, VirtualGamepadService virtualPad, KeyboardMouseInjector injector, MacroRunner? macros = null)
    {
        _engine = engine;
        _reader = reader;
        _virtualPad = virtualPad;
        _injector = injector;
        _macros = macros;
    }

    public bool IsRunning => _running;

    /// <summary>Raised on the polling thread after every processed frame.</summary>
    public event Action<RemappedState>? FrameProcessed;

    public bool HasPhysicalController { get; private set; }

    public void Start()
    {
        if (_running) return;
        _running = true;
        timeBeginPeriod(1); // default timer granularity is ~15ms, far too coarse for polling
        _thread = new Thread(Loop) { IsBackground = true, Name = "ReWASD input pipeline" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join();
        _thread = null;
        timeEndPeriod(1);
        _injector.ReleaseAll();
        _macros?.CancelAll();
    }

    private void Loop()
    {
        while (_running)
        {
            try
            {
                var physical = _reader.Read();
                HasPhysicalController = physical != null;

                if (physical != null)
                {
                    var state = _engine.ProcessInputs(
                        physical.Buttons,
                        physical.LeftStickX, physical.LeftStickY,
                        physical.RightStickX, physical.RightStickY,
                        physical.LeftTrigger, physical.RightTrigger);

                    _virtualPad.SendReport(state);
                    _injector.Apply(state);
                    _macros?.Update(state.ActiveMacros);
                    FrameProcessed?.Invoke(state);
                }
                else
                {
                    _injector.ReleaseAll();
                    _macros?.CancelAll();
                }
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
