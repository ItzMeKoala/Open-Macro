# Features

Everything below works on Windows and macOS unless it says otherwise. It is all set up in the window (Preferences holds most of
it) and saved with your configs. The ideas come from the feature list of
[ControllerKeys](https://github.com/NSEvent/xbox-controller-mapper), a macOS-only app; ReWASD's versions are written from scratch for
this project (ControllerKeys' source is under a non-commercial licence, so none of its code is used).

## Sticks

Each stick, in each config, can be a **controller stick** (as before), a **mouse cursor**, a **scroll wheel**, or **keys**
(W A S D, the arrow keys, or any keys, with diagonals). Cursor speed, scroll speed, how far to push before a key presses, and a
"slow down while holding" button (precision aiming) are settings. A layer can switch either stick's mode while it is active.
Deadzone (inner and outer), curve, sensitivity and flips are there too, with a live readout and **Measure drift** / **Measure reach**
buttons that set the deadzones for you.

## Macros

A macro is a list of steps written like `KeyTap:A; Delay:100; OpenUrl:https://example.com`. Besides keys, mouse and pad steps it can:

| Step | Does |
|---|---|
| `OpenUrl:https://…` | opens a web page |
| `LaunchApp:Safari` / `LaunchApp:notepad.exe` | starts an app or opens a file |
| `Shell:command` | runs a command without a window |
| `Webhook:POST https://… -H "Name=Value" {json}` | sends an HTTP request (GET, POST, PUT, DELETE, PATCH) |
| `Obs:Scene Gaming`, `Obs:record`, `Obs:stream`, `Obs:mute Mic`, `Obs:StartRecord` | controls OBS Studio (WebSocket 5; address and password in Preferences) |
| `TypeText:Hello` | types text (US layout) |

Write `\;` for a semicolon inside text. A failing web or OBS call is shown in the status bar and the macro carries on.

## Scripts

Pick **Script** next to Remap and Macro to give an input a small JavaScript program (run by Jint, nothing to install):
`press`, `hold`, `release`, `click`, `mouseDown`, `mouseUp`, `scroll`, `moveMouse`, `type`, `delay`, `shell`, `openURL`, `openApp`,
`webhook`, `obs`, `padPress`, `padHold`, `padRelease`, `notify`; `app.name`, `app.id`, `app.is('chrome')` for the app in front;
`state` keeps values between presses. Anything a script leaves held is let go when it ends, and letting go of a repeat-while-held
button always stops it.

## Combos

**Chords** (A + B together) and **sequences** (Up, Up, Down, Down) each run an action of their own. Type the buttons or press **Capture**
and use the controller. Buttons that belong to a chord wait a moment (the chord window) before acting alone.

## Command wheel

Hold a chosen button, point a stick at a slice, let go: the slice runs (any macro action). The wheel appears on top of the game without
taking focus. Up to eight slices per config.

## Profiles

- A profile's **target process** now works: while that app is in front the profile takes over, and the one you picked returns when you
  leave. Windows: the program name (`game.exe`) or path; macOS: the app name or bundle id (`Safari`, `com.apple.Safari`).
- **Import** and **Export** configs. A config from somebody else that can run commands, start programs, call web addresses, drive OBS or
  run scripts is listed for approval first.
- **Delete** a config; copies are kept (newest 20) before a config is deleted, replaced by a save, or restored over, and **History** in
  Preferences brings any of them back.
- **Presets**: couch mouse, WASD game, presentation clicker.

## Lock

Hold a set of buttons (View + Menu, Guide, both stick clicks…) for a second to lock the controller; again to unlock. While locked the
pad does nothing; keyboard and mouse mappings carry on.

## Overlay

A small always-on-top window showing the animated controller, for streams (use a window capture in OBS). Drag it, resize it from its
corner. Controllers without a picture show the pressed buttons as text.

## Background

A menu bar (macOS) / notification-area (Windows) icon: show the window, start or stop remapping, lock, overlay, quit. **Keep running when
the window is closed** makes the close button hide the window; on macOS the Dock icon can be hidden too.

## Usage counts

Preferences shows how often each button was pressed, how many macros ran and for how long remapping was on. Counted on this computer
only, never sent anywhere; reset any time.

## Updates

See the README: every push to `main` publishes a release and installed copies offer to update themselves.

## Not included (and why)

These ControllerKeys features are not in ReWASD:

- **Gyro aiming and touchpad mouse** (DualSense, DualShock 4, Steam Controller): needs raw device access that differs per system and
  controller, and could not be tried without the hardware.
- **Steam Controller, Siri Remote, 8BitDo Zero 2 / Micro / Ultimate 2 special handling, S29 ring, DualSense LEDs, microphone and battery
  notices, Xbox Elite paddles on macOS**: hardware-specific.
- **On-screen keyboard with swipe typing, laser pointer, cursor hints, directory navigator**.
- **Mac-to-Mac controller relay, linked controllers** (ReWASD reads one controller at a time), **continuous trigger MIDI**,
  **Stream Deck import**, **community profile browser**, **translations**.
- **Realtime latency mode**: ReWASD already sends simple mappings as key-down and key-up straight away; only buttons that are part of a
  chord wait (the chord window), which you can shorten.
