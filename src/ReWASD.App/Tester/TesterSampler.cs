using System.Diagnostics;
using System.Runtime.InteropServices;
using ReWASD.Engine.Input;

namespace ReWASD.UI.Tester;

/// <summary>
/// Reads the controller as fast as it will go while the tester is open, so the report-rate and timing figures mean something (the
/// screen only redraws sixty times a second, a pad can report several times that). The newest reading is always available.
/// </summary>
public sealed class TesterSampler : IDisposable
{
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

    private readonly Func<PhysicalState?> _read;
    private readonly TesterModel _model;
    private Thread? _thread;
    private volatile bool _running;
    private volatile PhysicalState? _latest;

    public TesterSampler(Func<PhysicalState?> read, TesterModel model)
    {
        _read = read;
        _model = model;
    }

    public static double NowMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    public bool IsRunning => _running;

    /// <summary>The newest reading (null when no controller is connected).</summary>
    public PhysicalState? Latest => _latest;

    public void Start()
    {
        if (_running) return;
        _running = true;
        if (OperatingSystem.IsWindows()) timeBeginPeriod(1);        // the default timer step is ~15 ms, far too coarse here
        _thread = new Thread(Loop) { IsBackground = true, Name = "ReWASD tester sampler" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join();
        _thread = null;
        if (OperatingSystem.IsWindows()) timeEndPeriod(1);
        _latest = null;
    }

    private void Loop()
    {
        while (_running)
        {
            try
            {
                var state = _read();
                _latest = state;
                _model.Feed(state, NowMs());
            }
            catch (Exception)
            {
                // One bad reading must not end the test
            }
            Thread.Sleep(1);
        }
    }

    public void Dispose() => Stop();
}
