using System.Runtime.Versioning;
using ReWASD.Engine.Input;

namespace ReWASD.Engine.Platform.MacOS;

/// <summary>
/// Shakes the controller in use through GameController's haptics (CoreHaptics), reached through the Objective-C runtime like
/// the reader. Xbox and DualSense pads have a motor in each handle; a pad with a single haptic channel is shaken as a whole.
/// Pads macOS cannot vibrate (some third-party and Switch pads) simply do nothing.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacRumbleOutput : IRumbleOutput, IDisposable
{
    /// <summary>A haptic event lasts at most 30 seconds, so a longer shake is started again before then.</summary>
    private const double EventSeconds = 30;
    private const double RestartSeconds = 25;
    private const double LevelStep = 0.02;

    private readonly MacGameController _pad;
    private readonly bool _available;
    private readonly object _gate = new();

    private IntPtr _controller;
    private Motor? _left, _right, _whole;
    private bool _split;

    public MacRumbleOutput(MacGameController pad)
    {
        _pad = pad;
        try
        {
            _available = pad.IsAvailable && ObjC.LoadFramework("CoreHaptics") && ObjC.Class("CHHapticEngine") != IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _available = false;
        }
    }

    public void Rumble(double left, double right)
    {
        if (!_available) return;
        left = double.IsNaN(left) ? 0 : Math.Clamp(left, 0, 1);
        right = double.IsNaN(right) ? 0 : Math.Clamp(right, 0, 1);

        lock (_gate)
        {
            try
            {
                if (!_pad.WithActiveController(controller => Apply(controller, left, right)))
                    Forget(); // the pad went away: its motors went with it
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                Forget();
            }
        }
    }

    private void Apply(IntPtr controller, double left, double right)
    {
        if (controller != _controller)
        {
            Forget();
            if (left <= 0 && right <= 0) return; // nothing to start on a pad that was never shaken
            Open(controller);
        }

        if (_split)
        {
            _left?.Set(left);
            _right?.Set(right);
        }
        else
        {
            _whole?.Set(Math.Max(left, right));
        }
    }

    /// <summary>A steady shake at this strength (0 to 1), autoreleased; zero when CoreHaptics refuses it. Needs an autorelease pool.</summary>
    internal static IntPtr BuildPattern(double level)
    {
        var parameters = ObjC.NewArray();
        ObjC.Add(parameters, ObjC.NewHapticParameter(ObjC.StringConstant("CHHapticEventParameterIDHapticIntensity", "HapticIntensity"), (float)level));
        ObjC.Add(parameters, ObjC.NewHapticParameter(ObjC.StringConstant("CHHapticEventParameterIDHapticSharpness", "HapticSharpness"), 0.5f));

        var events = ObjC.NewArray();
        ObjC.Add(events, ObjC.NewHapticEvent(ObjC.StringConstant("CHHapticEventTypeHapticContinuous", "HapticContinuous"), parameters, 0, EventSeconds));
        return ObjC.NewHapticPattern(events, ObjC.NewArray());
    }

    /// <summary>Gets the engines for this controller: one per handle when it has them, otherwise one for the whole pad.</summary>
    private void Open(IntPtr controller)
    {
        _controller = controller;
        var haptics = ObjC.RespondsTo(controller, "haptics") ? ObjC.Get(controller, "haptics") : IntPtr.Zero;
        if (haptics == IntPtr.Zero || !ObjC.RespondsTo(haptics, "createEngineWithLocality:")) return;

        var localities = ObjC.Get(haptics, "supportedLocalities");
        var leftName = ObjC.StringConstant("GCHapticsLocalityLeftHandle", "GCHapticsLocalityLeftHandle");
        var rightName = ObjC.StringConstant("GCHapticsLocalityRightHandle", "GCHapticsLocalityRightHandle");

        if (ObjC.SetContains(localities, leftName) && ObjC.SetContains(localities, rightName))
        {
            _left = Motor.Create(haptics, leftName);
            _right = Motor.Create(haptics, rightName);
            _split = _left != null && _right != null;
            if (_split) return;
            Forget();
            _controller = controller;
        }

        _whole = Motor.Create(haptics, ObjC.StringConstant("GCHapticsLocalityDefault", "GCHapticsLocalityDefault"));
    }

    private void Forget()
    {
        _left?.Dispose();
        _right?.Dispose();
        _whole?.Dispose();
        _left = _right = _whole = null;
        _split = false;
        _controller = IntPtr.Zero;
    }

    public void Dispose()
    {
        lock (_gate) Forget();
    }

    /// <summary>One haptic engine and the shake playing on it.</summary>
    private sealed class Motor : IDisposable
    {
        private readonly IntPtr _engine;
        private IntPtr _player;
        private double _level;
        private DateTime _started;

        private Motor(IntPtr engine) => _engine = engine;

        public static Motor? Create(IntPtr haptics, IntPtr locality)
        {
            using var scope = new Scope();
            var engine = ObjC.WithArgument(haptics, "createEngineWithLocality:", locality);
            if (engine == IntPtr.Zero) return null;
            ObjC.Retain(engine);
            // Left to itself the engine shuts down when idle and has to be started again before every shake
            if (ObjC.RespondsTo(engine, "setAutoShutdownEnabled:")) ObjC.SetBool(engine, "setAutoShutdownEnabled:", false);
            return new Motor(engine);
        }

        public void Set(double level)
        {
            if (level < LevelStep) level = 0;
            bool unchanged = Math.Abs(level - _level) < LevelStep && (level == 0) == (_level == 0);
            if (unchanged && (level == 0 || (DateTime.UtcNow - _started).TotalSeconds < RestartSeconds)) return;

            using var scope = new Scope();
            StopPlayer();
            _level = level;
            if (level == 0) return;

            if (!ObjC.CallWithError(_engine, "startAndReturnError:")) { _level = 0; return; }

            var pattern = BuildPattern(level);
            var player = pattern == IntPtr.Zero ? IntPtr.Zero : ObjC.WithArgumentAndError(_engine, "createPlayerWithPattern:error:", pattern);
            if (player == IntPtr.Zero) { _level = 0; return; }

            ObjC.Retain(player);
            if (!ObjC.CallAtTime(player, "startAtTime:error:", 0))
            {
                ObjC.Release(player);
                _level = 0;
                return;
            }

            _player = player;
            _started = DateTime.UtcNow;
        }

        private void StopPlayer()
        {
            if (_player == IntPtr.Zero) return;
            ObjC.CallAtTime(_player, "stopAtTime:error:", 0);
            ObjC.Release(_player);
            _player = IntPtr.Zero;
        }

        public void Dispose()
        {
            using var scope = new Scope();
            StopPlayer();
            ObjC.Release(_engine);
        }
    }

    private readonly struct Scope : IDisposable
    {
        private readonly IntPtr _pool;
        public Scope() => _pool = ObjC.objc_autoreleasePoolPush();
        public void Dispose() => ObjC.objc_autoreleasePoolPop(_pool);
    }
}
