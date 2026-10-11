# Running on macOS

ReWASD runs on macOS 12 or later (Apple silicon and Intel) with its own front end, built with Avalonia, on the same
engine and the same saved configs as Windows.

## Install and run

```sh
scripts/package-macos.sh          # builds artifacts/macos/<cpu>/ReWASD.app (the .NET runtime is bundled)
open artifacts/macos/osx-arm64/ReWASD.app
```

To run from source instead: `dotnet run --project src/ReWASD.UI.Mac` (needs the .NET 9 SDK).

## Permissions

macOS only lets an app watch the keyboard and mouse, or press keys for you, after you allow it. Open
**System Settings → Privacy & Security** and turn ReWASD on under both:

- **Accessibility**: to press keys and move the mouse, and to swallow the originals of remapped keys.
- **Input Monitoring**: to see real key and mouse presses.

When started from a terminal (`dotnet run`), the permission belongs to that terminal app. The app shows
"Permissions: needed" in the bottom bar until both are granted, and **Apply** asks for them. After granting, restart
the app. The `.app` build is ad-hoc signed, so rebuilding it may ask for the permissions again.

## What works

| | macOS |
|---|---|
| Controller buttons, sticks and triggers → keyboard keys, mouse buttons, scrolling, layers, macros | Yes |
| Keyboard and mouse as sources, macro recording, activators (single, long, double…), shift layers, turbo and toggle | Yes |
| Saved configs (`~/Library/Application Support/ReWASD-OpenSource/profiles`) | Yes, the same JSON files as on Windows |
| Panic key **Control + Option + Shift + F12** (hold `fn` on laptops with media keys) | Yes |
| Virtual Xbox 360 / DualShock 4 controller | **No.** macOS has no driver that can present one, so a mapping to another pad button does nothing |
| Hiding the physical controller from other apps (HidHide) | **No** |
| Vibration macro steps | **No** |

Controllers are read through Apple's GameController framework, so any pad macOS lists under
**System Settings → Game Controllers** works (Xbox, DualShock 4, DualSense, Switch Pro and MFi pads). The app asks
macOS to keep delivering controller input while another app has focus. This has not been tried with real controllers
yet; please report what you see.

## The PlayStation 5 picture

Pick **PlayStation 5** in the picture menu (it is chosen for you when a DualSense is connected) to get a detailed,
animated DualSense drawing. It follows your real controller: buttons sink and glow while held, triggers pull down
with the analog value, sticks lean and shrink when clicked, and the button you are editing is outlined in gold.
Click any part of the drawing to select it. The drawing is `src/ReWASD.Core/Skins/Art/dualsense.svg`; it opens in a
browser and carries the part names and motion settings as `data-*` attributes (see `SvgArtParser`). Other pictures
and the Windows app still use the outline drawings.

## Differences to know about

- Key names in configs are the Windows ones; `Alt` is Option, `Back` is the Delete key, `Delete` is forward delete
  and `Insert` is Help. F21–F24 do not exist on a Mac and are ignored.
- Keys are positions on a US keyboard, so `W` is the key where W sits on a US layout, whatever layout is active.
- Swallowing a modifier key also removes it from the keys pressed after it.

## Layout of the code

- `src/ReWASD.Engine/Platform/PlatformServices.cs` picks the Windows or macOS pieces.
- `src/ReWASD.Engine/Platform/MacOS/` has the macOS pieces: `MacGameController`, `MacInputHook`, `MacInputBackend`,
  `MacKeyCodes`, `MacPermissions`.
- `src/ReWASD.Engine/Input/InputHookCore.cs` is the hook logic both platforms share.
- `src/ReWASD.App/` holds the view models both front ends share; `src/ReWASD.UI` is WPF (Windows) and
  `src/ReWASD.UI.Mac` is Avalonia (macOS).
