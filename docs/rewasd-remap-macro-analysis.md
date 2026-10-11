# How reWASD remapping and macros work: evidence and design mapping

Method: static inspection only, with REA and byte-level reads. Nothing was executed, no code was decompiled, and no
reWASD code, assets or schema text is reproduced here. Only the **names, enums, ranges and structure** of the profile data
format are summarised, which is what an interoperable, clean-room implementation needs.

Target: reWASD **9.5.0.11346**, `C:\Program Files\reWASD` (managed .NET 10 mixed-mode assemblies plus one NativeAOT tool).

| Artifact | SHA-256 | REA evidence |
|---|---|---|
| `Engine.dll` | `cc97ce57…a8aa9` | `ev_19eb5f29fceded4752984472e51af2f7b1e4479cf159a796b1e5c7dded2a15f1` |
| `reWASDUI.dll` | `8c44e126…a067a` | `ev_6e284a5d36af29d21e80562ce2c2f88a02f0295257b72e0e5b848ecbe38ff67a` |
| `Resources.dll` (WPF artwork only) | `4e0a17a5…7080f` | `ev_5759ed51fcfa6644b4d493ab7ea8e3d47322c136e4f5a75dcf8d638497300bcb` |
| `Common.dll` | `4a228f81…febe5a` | metadata strings read directly |
| `Engine.NET.Base.dll` | `dbb852e5…c200c79` | metadata strings read directly |
| `rcompiler.exe` | `b9f25068…b291cd` | strings read directly |
| Embedded profile JSON Schema (draft-07, "Schema for reWASD profile", `schemaVersion` 3) | text sha256 `09a09985…385e234` | found at file offset 3,377,002 of `reWASDUI.dll`, 74,151 bytes |

## 1. Architecture (observed)

- The schema is embedded in the UI assembly. The UI edits a profile as JSON.
- `rcompiler.exe` (`rwsdcompiler`, NativeAOT) contains `MacroCompilers`, `MacroCompiler`, `MacroCompilerActivatorBinding`,
  `MacroCompilerButtonBinding`, `MacroCompilerVirtualMouse` and native structure names `REWASD_BUTTON_MASK`,
  `REWASD_BUTTON_MACRO` and `REWASD_CONTROLLER_PROFILE`. It also carries `DISC_SOFT_KERNEL_REMAP_Gamepad` and
  `…_Keyboard`. **Inference:** the JSON profile is compiled to a binary table that a kernel-mode component executes.
  Remapping itself is not done in the UI process.
- `Common.dll` has `_compiledPressMacroBytes` and `_compiledReleaseMacroBytes`, so each activator has a **press** macro and a
  **release** macro, plus counters per activator kind (`Start`, `Release`, `ZoneLow/Med/High`).
- `Engine.dll` has `EnableRemap`, `DisableRemap`, `RemapOffCommand`, `autoRemap`, `curRemapedForegroundName` and
  `DisableCurrentAppRemapOnFocusLostIfNeeded`. Remapping can be switched on or off, and applied automatically for the
  foreground process. The schema's `config.processNames` is the matching list.

## 2. The mapping model (observed, from the schema)

A **mapping** = *condition* + *outcome*.

**Condition**
- A hardware source on a device (`deviceId`): a `buttonId` (1…367, one id space shared by gamepad, keyboard and mouse
  devices), a stick axis (`stick`, `axis`) or a `trigger`.
- Optional `shiftId` (1…12): the mapping applies only while that shift layer is active.
- Or a **mask** (a set of up to 4 buttons) used as a chord source.

**Outcome: hardware remap**
- Another `buttonId`.
- A stick axis (with `inverted`).
- A trigger.
- `unmap: true`, which removes the original input.

**Outcome: macros**
- An ordered list of steps (see §4), a `mouseRegion` (x1,y1,x2,y2), and/or `jumpToLayer {layer, delay, postpone}`.

**Activator**: *when* the mapping fires.

| Field | Values (observed) |
|---|---|
| `type` | `single`, `long`, `double`, `triple`, `start`, `release`, `zoneLow`, `zoneMed`, `zoneHigh` |
| `mode` | `onetime`, `repeat`, `toggle`, plus `hold_until_release` and `turbo` (not allowed for `release`) |
| `params` | free key/value; `rcompiler` exposes `SingleWaitTime`, `DoubleWaitTime`, `LongWaitTime` |

Zone activators are for analog inputs (low/medium/high travel).

## 3. Shifts and masks (observed)

- 12 shifts. `type`: `default`, `toggle`, `unlock`, `radialMenu`, `unlockKeyboardOnly`, `unlockMouseOnly`.
- A shift's `set`, and a mask's `set`, hold **1 to 4** buttons (`deviceId` + `buttonId`), so a shift or mask is a chord.
- `unheritableMasks` lists masks that a shift does not inherit.
- Config option `minTimeUnit` is 1 or 10 (default 10), the macro time granularity in ms.

## 4. Macro steps (observed)

| Step | Fields and ranges |
|---|---|
| `keyboard` | `buttonId`, `action` = `down` / `single_down` / `up`, `pulsation` |
| `mouse` | button 1–5 with `down`/`up`; or `direction` (up/down/left/right) + `value` + `flick`; or `wheel` direction + `value` |
| `gamepad` | `id`, `buttonId`; analog ids take `value`, `absolute` (set vs relative), `compensation`, `action`, `pulsation`; triggers (ids 51, 55) take `value`, `absolute`; other ids take `action` + `value` |
| `rumble` | `duration`, `amplitude` per `motor`/`trigger` × `left`/`right`, 0–100 |
| `pause` | `value` (ms, > 0) |
| `break` | no fields |
| `command` | `id` of a user command |

UI-side names agree: macro editors exist for gamepad, keyboard and mouse. Template names include `MacroBaseDuration`,
`MacroBreak`, `MacroCrutchHoldUntilRelease`, `MacroGamepadAxis`, `MacroGamepadKey`, `MacroGamepadStickCompensation`,
`MacroKeyBinding`, `MacroPause`, `MacroRumble`. A `MacroRecorder` records live key and mouse events. There is a
configurable "stop macro" hotkey (`STR_STOP_MACRO_HOTKEY`). `_preventSleepIfComboIsBeingEmulated` shows that a running
macro keeps the machine awake.

## 5. Virtual devices and stick handling (observed)

- Virtual gamepad `type`: `360`, `ds4`, `xboxonebt`, `nintendoswitchpro`, `ds3`, `keyboardandmouse`.
- Hardware gamepad options: `convertToVirtual`, `applyToPhysicalPriority`, `hidePhysicalGamepad`.
- Virtual keyboard: `repeatMode` (`random` | `fixed`) and `repeatValue`. This is the key-repeat interval used by Turbo.
- Virtual mouse (stick to mouse): `sensitivity` (1–100), `sensitivityY`, `smoothing`, `deflection`, `acceleration`,
  `wheelDeflection`, flick settings.
- Stick: `sensitivity` x/y, `rotation` (−90…90), response `curves` (4 points), `zones`, `diagonalZones`.

## 6. Inferred behavior (not directly observed)

1. **Single vs. double/triple/long.** When a button has `double`, `triple` or `long` activators, `single` must wait to see
   whether another press follows. This is why `SingleWaitTime` and `DoubleWaitTime` exist, and why a message like
   `STR_CANT_DELAY_FOR_START_ACTIVATOR` exists: `start` fires at press time and cannot be delayed.
2. **Modes.** `onetime` fires the outcome once per activation; `repeat`/`turbo` repeats while held; `toggle` latches until
   the next activation; `hold_until_release` keeps the outcome held for as long as the source is held.
3. **Turbo and Toggle limit combos.** An observed UI string says "Turbo and Toggle set limits on Combo", so macros with
   those modes cannot use their full sequencing features.
4. **Layering.** Shift activation selects which mapping set applies; `jumpToLayer` switches layer from a mapping.

## 7. Unknown

- Default wait times and the exact tick semantics of `minTimeUnit`.
- Exact meaning of `single_down` versus `down`, and of `pulsation`, `flick` and `compensation`.
- What `break` does precisely (end the macro, or exit a loop).
- User-facing wording: the localised UI text was not found in a readable form.

## 8. How this project mirrors it

| Observed concept | This project | Status |
|---|---|---|
| Activators `single/long/double/triple/start/release` | `ActivatorType`, `ActivatorBinding`, per-source tap/hold state machine in `MappingEngine` | done, unit-tested |
| Modes `onetime/repeat/toggle/hold_until_release` | `MappingMode` `OneTime`, `Turbo` (repeat), `Toggle`, `Normal` (hold) | done, unit-tested |
| Configurable wait times | `ActivatorTimings` on the profile (sliders in Preferences) | done; the defaults are our own choice |
| Keyboard, mouse and gamepad share one mapping model | pad, key and mouse sources go through the same engine | done |
| Capturing the real keyboard/mouse and `unmap` | `LowLevelInputHook`: a mapped key or button is swallowed, everything else passes through | done; hook tested with an injected F13 |
| Chord shifts (1–4 buttons), `toggle` shift | `ShiftLayer.ModifierButtons`, `ShiftType` | done, unit-tested; the buttons used for a chord are consumed |
| Shifts inherit from the base layer (`unheritableMasks` implies it) | a shift layer falls back to Main for anything it does not override | done; `unheritableMasks` itself is not modelled |
| `jumpToLayer` | `TargetType.JumpToLayer` (no `delay`/`postpone`) | partial |
| Macro steps: keyboard, mouse (button/move/wheel), gamepad button/axis, rumble, pause, break | `MacroActionType` + `MacroRunner`, `MacroPadState`, `XInputRumbleOutput` | done except `pulsation`, `flick`, `compensation`, `command` |
| `mouseRegion` | not implemented | open |
| Per-app auto remap (`processNames`) | `Profile.ProcessExecutable` is stored but not acted on | open |
| Masks (button chords as a source), analog zone activators, stick-to-mouse, radial menu | not implemented | open |
| Virtual gamepad types other than 360 | not implemented | open |

Safety rails added for the hook (not from reWASD): only mapped inputs are swallowed, events this process injects are
ignored, a swallowed key's release is swallowed with it, and Ctrl+Alt+Shift+F12 always stops remapping.
