# UI structure (observed) and how this project maps it

Source of observations: static inspection of an installed reWASD 9.5.0.11346 with REA, no code executed.
`reWASDUI.dll` (evidence `ev_6e284a5d36af29d21e80562ce2c2f88a02f0295257b72e0e5b848ecbe38ff67a`) is a WPF app whose
`reWASDUI.g.resources` lists 600+ compiled view names. Only the **names and nesting** were used. No XAML, images or
other assets were copied; every view in this repo is written from scratch.

## Observed shell

| Observed view group | Meaning (inferred from names) | Our counterpart |
|---|---|---|
| `NavigationPanel`, `MainWindow`, `MainContent` | Left navigation and page host | Left column with Remap / Preferences switch |
| `GamesSelector`, `GameConfigSelector`, `AddEditGame` | Per-game configs bound to a process | Config list + target-process box |
| `GamepadSelector`, `GamepadSlotProfiles` | Device and slot selection | Slot buttons, connected-device list |
| `ShiftButtonUC`, `AddShiftButton`, `ShiftSelectorControl` | Shift layers | Config layer bar |
| `BindingFrame` + `BF*` views | Per-button editor with tabs | Tab strip + mapping inspector |
| `Preferences*` | Settings pages | Preferences page (drivers, output) |

## Binding-frame tabs → features (enable one at a time)

| Tab | Observed views | Status here |
|---|---|---|
| Main | `BFMain`, `BFGamepadMapping`, `TurboToggle` | Implemented |
| Shift | `BFShift` | Layers exist in the engine; tab not wired |
| Advanced | `BFAdvanced`, `BFStickAdvancedTemplate`, `StickSensitivity*`, `ZoneMultiSlider` | Not started |
| Keyboard | `BFKeyboard`, `KeyboardMappingView` | Key targets exist in the Main tab |
| Rumble | `BFRumble`, `VibrationSettings` | Not started |
| Mask | `BFMask`, `MaskView` | Not started |

## Not planned

License, login, update, telemetry (LaunchDarkly/Application Insights) and the external-device wizards (ESP32, PS4/Switch
consoles, GIMX) are out of scope.
