using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReWASD.Engine.Input;

/// <summary>
/// Watches the real keyboard and mouse so they can be used as remapping sources, and swallows the keys and
/// buttons that have a mapping. Events this process injects itself are always ignored, so output is never
/// read back in as input.
///
/// Safety: only inputs the engine reports as mapped are swallowed. Ctrl+Alt+Shift+F12 is a panic key that is
/// never forwarded to the engine and raises <see cref="PanicRequested"/>.
/// </summary>
public sealed class LowLevelInputHook : IDisposable
{
    public static readonly string[] MouseNames =
    {
        "Left Click", "Right Click", "Middle Click", "Mouse Button 4", "Mouse Button 5", "Wheel Up", "Wheel Down"
    };

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;

    private const int WmKeyDown = 0x0100, WmKeyUp = 0x0101, WmSysKeyDown = 0x0104, WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201, WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204, WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207, WmMButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B, WmXButtonUp = 0x020C;
    private const int WmQuit = 0x0012;

    private const uint LlkhfInjected = 0x10;
    private const uint LlmhfInjected = 0x01;
    private const int WheelHoldMs = 60;
    private const ushort VkF12 = 0x7B;

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public int x;
        public int y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc proc, IntPtr hMod, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Msg msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);

    private readonly Func<string, bool> _isKeySuppressed;
    private readonly Func<string, bool> _isMouseSuppressed;
    private readonly object _gate = new();
    private readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pressedMouse = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _wheelUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _swallowedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _swallowedMouse = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool[] _modifiers = new bool[3]; // ctrl, alt, shift

    // Delegates are fields so the GC cannot collect them while Windows still holds the pointers
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;

    public LowLevelInputHook(Func<string, bool> isKeySuppressed, Func<string, bool> isMouseSuppressed)
    {
        _isKeySuppressed = isKeySuppressed;
        _isMouseSuppressed = isMouseSuppressed;
    }

    /// <summary>When true (tests only), events injected by this or any process are processed instead of ignored.</summary>
    public bool IncludeInjected { get; set; }

    /// <summary>True once both hooks are installed.</summary>
    public bool IsInstalled { get; private set; }

    /// <summary>Raised on a thread-pool thread when the panic hotkey (Ctrl+Alt+Shift+F12) is pressed.</summary>
    public event Action? PanicRequested;

    public bool Start()
    {
        if (_running) return IsInstalled;

        using var ready = new ManualResetEventSlim(false);
        _running = true;

        _thread = new Thread(() => MessageLoop(ready)) { IsBackground = true, Name = "ReWASD input hook" };
        _thread.Start();

        ready.Wait(TimeSpan.FromSeconds(3));
        if (!IsInstalled) Stop();
        return IsInstalled;
    }

    public void Stop()
    {
        if (!_running && _thread == null) return;
        _running = false;

        if (_threadId != 0) PostThreadMessageW(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(2000);
        _thread = null;
        _threadId = 0;
        IsInstalled = false;

        lock (_gate)
        {
            _pressedKeys.Clear();
            _pressedMouse.Clear();
            _wheelUntil.Clear();
            _swallowedKeys.Clear();
            _swallowedMouse.Clear();
            Array.Clear(_modifiers);
        }
    }

    /// <summary>Keys and mouse buttons (and recent wheel notches) currently down, by profile name.</summary>
    public (IReadOnlyCollection<string> Keys, IReadOnlyCollection<string> Mouse) Snapshot()
    {
        lock (_gate)
        {
            long now = Environment.TickCount64;
            foreach (var expired in _wheelUntil.Where(w => w.Value <= now).Select(w => w.Key).ToList())
            {
                _wheelUntil.Remove(expired);
                _pressedMouse.Remove(expired);
            }

            return (_pressedKeys.ToArray(), _pressedMouse.ToArray());
        }
    }

    private void MessageLoop(ManualResetEventSlim ready)
    {
        IntPtr keyboardHook = IntPtr.Zero, mouseHook = IntPtr.Zero;
        try
        {
            _threadId = GetCurrentThreadId();
            _keyboardProc = KeyboardCallback;
            _mouseProc = MouseCallback;

            IntPtr module = GetModuleHandleW(null);
            keyboardHook = SetWindowsHookExW(WhKeyboardLl, _keyboardProc, module, 0);
            mouseHook = SetWindowsHookExW(WhMouseLl, _mouseProc, module, 0);
            IsInstalled = keyboardHook != IntPtr.Zero && mouseHook != IntPtr.Zero;
            ready.Set();

            if (!IsInstalled) return;

            // Low-level hooks are called through this thread's message loop
            while (_running && GetMessageW(out _, IntPtr.Zero, 0, 0) > 0)
            {
            }
        }
        finally
        {
            if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
            if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
            ready.Set();
        }
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && _running)
            {
                var kb = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                bool injected = (kb.flags & LlkhfInjected) != 0;
                int message = wParam.ToInt32();
                bool down = message is WmKeyDown or WmSysKeyDown;
                bool up = message is WmKeyUp or WmSysKeyUp;

                if ((!injected || IncludeInjected) && (down || up) && HandleKey((ushort)kb.vkCode, down)) return (IntPtr)1;
            }
        }
        catch (Exception)
        {
            // Never let a bug in our handling block the user's keyboard
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    /// <summary>Returns true if the event should be swallowed.</summary>
    private bool HandleKey(ushort vk, bool down)
    {
        // Track modifiers for the panic key before anything else
        switch (vk)
        {
            case 0x10 or 0xA0 or 0xA1: lock (_gate) _modifiers[2] = down; break;
            case 0x11 or 0xA2 or 0xA3: lock (_gate) _modifiers[0] = down; break;
            case 0x12 or 0xA4 or 0xA5: lock (_gate) _modifiers[1] = down; break;
        }

        if (vk == VkF12)
        {
            bool panic;
            lock (_gate) panic = _modifiers[0] && _modifiers[1] && _modifiers[2];
            if (panic)
            {
                if (down) ThreadPool.QueueUserWorkItem(_ => PanicRequested?.Invoke());
                return true; // the panic combo itself is never passed on
            }
        }

        if (!KeyMap.TryGetKeyName(vk, out var name)) return false;

        lock (_gate)
        {
            if (down)
            {
                _pressedKeys.Add(name);
                if (_swallowedKeys.Contains(name)) return true;       // auto-repeat of a key we are already swallowing
                if (!_isKeySuppressed(name)) return false;
                _swallowedKeys.Add(name);
                return true;
            }

            _pressedKeys.Remove(name);
            return _swallowedKeys.Remove(name); // a swallowed key's release is swallowed too, so nothing is left stuck
        }
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && _running)
            {
                var ms = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
                bool injected = (ms.flags & LlmhfInjected) != 0;
                if ((!injected || IncludeInjected) && HandleMouse(wParam.ToInt32(), ms.mouseData)) return (IntPtr)1;
            }
        }
        catch (Exception)
        {
            // Never let a bug in our handling block the user's mouse
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool HandleMouse(int message, uint mouseData)
    {
        string? name;
        bool down;

        switch (message)
        {
            case WmLButtonDown: name = "Left Click"; down = true; break;
            case WmLButtonUp: name = "Left Click"; down = false; break;
            case WmRButtonDown: name = "Right Click"; down = true; break;
            case WmRButtonUp: name = "Right Click"; down = false; break;
            case WmMButtonDown: name = "Middle Click"; down = true; break;
            case WmMButtonUp: name = "Middle Click"; down = false; break;
            case WmXButtonDown or WmXButtonUp:
                name = (mouseData >> 16) == 1 ? "Mouse Button 4" : "Mouse Button 5";
                down = message == WmXButtonDown;
                break;
            case WmMouseWheel:
                short delta = (short)(mouseData >> 16);
                name = delta > 0 ? "Wheel Up" : "Wheel Down";
                lock (_gate)
                {
                    if (!_isMouseSuppressed(name)) return false;
                    _pressedMouse.Add(name);
                    _wheelUntil[name] = Environment.TickCount64 + WheelHoldMs;
                }
                return true;
            default:
                return false; // movement and everything else is left alone
        }

        lock (_gate)
        {
            if (down)
            {
                _pressedMouse.Add(name);
                if (_swallowedMouse.Contains(name)) return true;
                if (!_isMouseSuppressed(name)) return false;
                _swallowedMouse.Add(name);
                return true;
            }

            _pressedMouse.Remove(name);
            return _swallowedMouse.Remove(name);
        }
    }

    public void Dispose() => Stop();
}
