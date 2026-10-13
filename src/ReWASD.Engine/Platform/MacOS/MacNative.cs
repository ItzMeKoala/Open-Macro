using System.Runtime.InteropServices;

namespace ReWASD.Engine.Platform.MacOS;

[StructLayout(LayoutKind.Sequential)]
internal struct CGPoint
{
    public double X;
    public double Y;
    public CGPoint(double x, double y) { X = x; Y = y; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGSize
{
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGRect
{
    public CGPoint Origin;
    public CGSize Size;
}

/// <summary>Direct bindings to the few CoreGraphics / CoreFoundation / HIServices calls the macOS backends need.</summary>
internal static class MacNative
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    // CGEventType
    public const uint LeftMouseDown = 1, LeftMouseUp = 2, RightMouseDown = 3, RightMouseUp = 4, MouseMoved = 5;
    public const uint LeftMouseDragged = 6, RightMouseDragged = 7, KeyDown = 10, KeyUp = 11, FlagsChanged = 12;
    public const uint ScrollWheel = 22, OtherMouseDown = 25, OtherMouseUp = 26, OtherMouseDragged = 27;
    public const uint TapDisabledByTimeout = 0xFFFFFFFE, TapDisabledByUserInput = 0xFFFFFFFF;

    // CGEventField
    public const uint FieldMouseClickState = 1, FieldMouseButtonNumber = 3, FieldMouseDeltaX = 4, FieldMouseDeltaY = 5;
    public const uint FieldKeyboardAutorepeat = 8, FieldKeyboardKeycode = 9, FieldScrollDeltaAxis1 = 11;
    public const uint FieldSourceUnixProcessId = 41, FieldSourceUserData = 42, FieldScrollPointDeltaAxis1 = 96;

    // CGEventFlags
    public const ulong FlagShift = 0x20000, FlagControl = 0x40000, FlagAlternate = 0x80000, FlagCommand = 0x100000;

    // Device-dependent modifier bits (IOKit NX_DEVICE*KEYMASK): which side of the keyboard a modifier is held on
    public const ulong DeviceLShift = 0x2, DeviceRShift = 0x4, DeviceLControl = 0x1, DeviceRControl = 0x2000;
    public const ulong DeviceLAlt = 0x20, DeviceRAlt = 0x40;
    public const ulong DeviceLCommand = 0x8, DeviceRCommand = 0x10;

    public const uint HidEventTap = 0;
    public const uint SessionEventTap = 1;
    public const uint HeadInsertEventTap = 0;
    public const uint EventTapOptionDefault = 0;
    public const int EventSourceStateHidSystem = 1;
    public const int EventSourceStatePrivate = -1;
    public const uint ScrollEventUnitLine = 1;

    public delegate IntPtr EventTapCallback(IntPtr proxy, uint type, IntPtr evt, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventTapCreate(uint tap, uint place, uint options, ulong eventsOfInterest, EventTapCallback callback, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    public static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [DllImport(CoreGraphics)]
    public static extern long CGEventGetIntegerValueField(IntPtr evt, uint field);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetIntegerValueField(IntPtr evt, uint field, long value);

    [DllImport(CoreGraphics)]
    public static extern ulong CGEventGetFlags(IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetFlags(IntPtr evt, ulong flags);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventSourceCreate(int stateId);

    [DllImport(CoreGraphics)]
    public static extern ulong CGEventSourceFlagsState(int stateId);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort keyCode, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint position, uint button);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, uint units, uint wheelCount, int wheel1, int wheel2, int wheel3);

    [DllImport(CoreGraphics)]
    public static extern CGPoint CGEventGetLocation(IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern void CGEventPost(uint tap, IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern int CGGetActiveDisplayList(uint maxDisplays, [Out] uint[] displays, out uint displayCount);

    [DllImport(CoreGraphics)]
    public static extern CGRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CGPreflightListenEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CGRequestListenEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CGPreflightPostEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CGRequestPostEventAccess();

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool AXIsProcessTrusted();

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, long order);

    [DllImport(CoreFoundation)]
    public static extern void CFMachPortInvalidate(IntPtr port);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopRemoveSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    public static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);

    private static IntPtr _defaultMode;

    /// <summary>kCFRunLoopDefaultMode, a CFString constant exported by CoreFoundation.</summary>
    public static IntPtr DefaultRunLoopMode
    {
        get
        {
            if (_defaultMode == IntPtr.Zero)
            {
                var lib = NativeLibrary.Load(CoreFoundation);
                _defaultMode = Marshal.ReadIntPtr(NativeLibrary.GetExport(lib, "kCFRunLoopDefaultMode"));
            }
            return _defaultMode;
        }
    }
}

/// <summary>Minimal Objective-C runtime access, enough to poll the GameController framework without a native shim.</summary>
internal static class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    [DllImport(Lib)] private static extern IntPtr objc_getClass(string name);
    [DllImport(Lib)] private static extern IntPtr sel_registerName(string name);
    [DllImport(Lib)] private static extern IntPtr object_getClass(IntPtr obj);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool class_respondsToSelector(IntPtr cls, IntPtr sel);
    [DllImport(Lib)] public static extern IntPtr objc_autoreleasePoolPush();
    [DllImport(Lib)] public static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtr(IntPtr self, IntPtr sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrIndex(IntPtr self, IntPtr sel, nuint index);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nuint SendUInt(IntPtr self, IntPtr sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SendBool(IntPtr self, IntPtr sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern float SendFloat(IntPtr self, IntPtr sel);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern void SendVoidBool(IntPtr self, IntPtr sel, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrPtr(IntPtr self, IntPtr sel, IntPtr a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrPtrError(IntPtr self, IntPtr sel, IntPtr a, ref IntPtr error);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrPtrPtrError(IntPtr self, IntPtr sel, IntPtr a, IntPtr b, ref IntPtr error);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrPtrFloat(IntPtr self, IntPtr sel, IntPtr a, float value);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtrPtrPtrDoubleDouble(IntPtr self, IntPtr sel, IntPtr a, IntPtr b, double c, double d);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SendBoolError(IntPtr self, IntPtr sel, ref IntPtr error);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SendBoolDoubleError(IntPtr self, IntPtr sel, double time, ref IntPtr error);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SendBoolPtr(IntPtr self, IntPtr sel, IntPtr a);
    [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern void SendVoidPtr(IntPtr self, IntPtr sel, IntPtr a);

    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlopen(string path, int mode);
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlsym(IntPtr handle, string symbol);

    private static readonly Dictionary<string, IntPtr> Selectors = new();

    public static IntPtr Sel(string name)
    {
        lock (Selectors)
        {
            if (!Selectors.TryGetValue(name, out var sel)) Selectors[name] = sel = sel_registerName(name);
            return sel;
        }
    }

    public static IntPtr Class(string name) => objc_getClass(name);

    /// <summary>Loads a system framework so its classes are registered with the runtime.</summary>
    public static bool LoadFramework(string name) =>
        dlopen($"/System/Library/Frameworks/{name}.framework/{name}", 1) != IntPtr.Zero;

    public static IntPtr Get(IntPtr obj, string selector) => obj == IntPtr.Zero ? IntPtr.Zero : SendPtr(obj, Sel(selector));
    public static IntPtr GetAt(IntPtr obj, string selector, int index) => obj == IntPtr.Zero ? IntPtr.Zero : SendPtrIndex(obj, Sel(selector), (nuint)index);
    public static int Count(IntPtr array) => array == IntPtr.Zero ? 0 : (int)SendUInt(array, Sel("count"));
    public static bool GetBool(IntPtr obj, string selector) => obj != IntPtr.Zero && SendBool(obj, Sel(selector));
    public static float GetFloat(IntPtr obj, string selector) => obj == IntPtr.Zero ? 0f : SendFloat(obj, Sel(selector));

    /// <summary>True if the class itself (not its instances) implements a class method with this selector.</summary>
    public static bool ClassRespondsTo(IntPtr cls, string selector) =>
        cls != IntPtr.Zero && class_respondsToSelector(object_getClass(cls), Sel(selector));

    public static void SetBool(IntPtr obj, string selector, bool value)
    {
        if (obj != IntPtr.Zero) SendVoidBool(obj, Sel(selector), value);
    }

    public static string? GetString(IntPtr obj, string selector)
    {
        var nsString = Get(obj, selector);
        if (nsString == IntPtr.Zero) return null;
        var utf8 = Get(nsString, "UTF8String");
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    /// <summary>True if this object responds to the selector.</summary>
    public static bool RespondsTo(IntPtr obj, string selector) =>
        obj != IntPtr.Zero && class_respondsToSelector(object_getClass(obj), Sel(selector));

    public static IntPtr Retain(IntPtr obj) => Get(obj, "retain");
    public static void Release(IntPtr obj) => Get(obj, "release");
    public static IntPtr Autorelease(IntPtr obj) => Get(obj, "autorelease");
    public static IntPtr New(string className) => Autorelease(Get(Get(Class(className), "alloc"), "init"));

    /// <summary>An autoreleased NSString.</summary>
    public static IntPtr NSString(string text)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(text);
        try { return SendPtrPtr(Class("NSString"), Sel("stringWithUTF8String:"), utf8); }
        finally { Marshal.FreeCoTaskMem(utf8); }
    }

    /// <summary>The NSString constant a framework exports under this name (for example a CoreHaptics key), or the text itself when it cannot be found.</summary>
    public static IntPtr StringConstant(string symbol, string fallback)
    {
        var address = dlsym(new IntPtr(-2), symbol); // RTLD_DEFAULT: the frameworks are already loaded
        var value = address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
        return value != IntPtr.Zero ? value : NSString(fallback);
    }

    public static IntPtr NewArray() => Get(Class("NSMutableArray"), "array");
    public static void Add(IntPtr array, IntPtr item) => SendVoidPtr(array, Sel("addObject:"), item);
    public static bool SetContains(IntPtr set, IntPtr item) => set != IntPtr.Zero && SendBoolPtr(set, Sel("containsObject:"), item);

    /// <summary>[[Class alloc] initWithEventType:parameters:relativeTime:duration:], autoreleased.</summary>
    public static IntPtr NewHapticEvent(IntPtr type, IntPtr parameters, double relativeTime, double duration) =>
        Autorelease(SendPtrPtrPtrDoubleDouble(Get(Class("CHHapticEvent"), "alloc"), Sel("initWithEventType:parameters:relativeTime:duration:"), type, parameters, relativeTime, duration));

    /// <summary>[[CHHapticEventParameter alloc] initWithParameterID:value:], autoreleased.</summary>
    public static IntPtr NewHapticParameter(IntPtr id, float value) =>
        Autorelease(SendPtrPtrFloat(Get(Class("CHHapticEventParameter"), "alloc"), Sel("initWithParameterID:value:"), id, value));

    /// <summary>[[CHHapticPattern alloc] initWithEvents:parameters:error:], autoreleased; zero when it could not be made.</summary>
    public static IntPtr NewHapticPattern(IntPtr events, IntPtr parameters)
    {
        var error = IntPtr.Zero;
        return Autorelease(SendPtrPtrPtrError(Get(Class("CHHapticPattern"), "alloc"), Sel("initWithEvents:parameters:error:"), events, parameters, ref error));
    }

    public static IntPtr WithArgument(IntPtr obj, string selector, IntPtr argument) =>
        obj == IntPtr.Zero ? IntPtr.Zero : SendPtrPtr(obj, Sel(selector), argument);

    /// <summary>obj.selector(argument, error:), for example createPlayerWithPattern:error:. The error is dropped.</summary>
    public static IntPtr WithArgumentAndError(IntPtr obj, string selector, IntPtr argument)
    {
        var error = IntPtr.Zero;
        return obj == IntPtr.Zero ? IntPtr.Zero : SendPtrPtrError(obj, Sel(selector), argument, ref error);
    }

    /// <summary>obj.selector(error:), for example startAndReturnError:.</summary>
    public static bool CallWithError(IntPtr obj, string selector)
    {
        var error = IntPtr.Zero;
        return obj != IntPtr.Zero && SendBoolError(obj, Sel(selector), ref error);
    }

    /// <summary>obj.selector(time, error:), for example startAtTime:error:.</summary>
    public static bool CallAtTime(IntPtr obj, string selector, double time)
    {
        var error = IntPtr.Zero;
        return obj != IntPtr.Zero && SendBoolDoubleError(obj, Sel(selector), time, ref error);
    }
}
