# ReWASD Open Source

An open-source reverse-engineering and recreation effort for gamepad remapping, virtual device emulation, and macro processing.

## Getting Started with REA

This repository is integrated with [REA (Reverse Engineer Anything)](https://github.com/morluto/rea).

### Prerequisites
- **Node.js**: v24 LTS (or supported 22/26+)
- **REA CLI**: Installed globally via `npm install --global rea-agents`
- **Agent Integration**: Antigravity is configured with the REA MCP server (`~/.gemini/config/mcp_config.json`) and the `reverse-engineer-anything` skill.

### Verification
Run REA diagnostic to ensure alignment:
```powershell
rea doctor --client antigravity
```

### Investigating Targets with REA
When working with AI agents in this repository, you can provide target binaries (such as installer files, DLLs, virtual gamepad drivers, or application executables) and prompt:
```text
Inspect the target binary [path/to/binary.dll] and explain how virtual gamepad bus communication (IOCTLs) and input remapping hooks are handled.
```

The agent will automatically invoke REA's MCP tools (or the `rea` CLI) to decompile, map imports/exports, trace call trees, and extract clean implementation specifications.

### Get the app

**Easiest:** open the [latest release](https://github.com/ItzMeKoala/ReWASD-Open-Source/releases/tag/latest) and download

- Windows: `ReWASD-win-x64.exe` — double-click it (nothing to install).
- Mac: `ReWASD-osx-arm64.zip` (Apple silicon) or `ReWASD-osx-x64.zip` (Intel) — unzip it and open `ReWASD.app` (the first time: right-click, then Open).

The release is rebuilt automatically after every change to `main`. An installed copy checks for a newer build when it starts and shows
an **Update available** button; one click downloads it, replaces the app and restarts it (Preferences has a switch for the check).

**Or build it yourself** (needs the .NET 9 SDK):

- Windows: double-click `Build.cmd` (or run `.\build.ps1`) → `dist\ReWASD.exe`.
- Mac: run `./build.sh` → `dist/ReWASD.app`.
- `./build.sh windows` makes the Windows exe from a Mac too.

The bottom bar shows the version and git commit a build was made from, so you can tell whether you are on the latest. A virtual controller
on Windows needs the ViGEmBus driver; hiding the pad needs HidHide. See [docs/macos.md](docs/macos.md) for the Mac.

```sh
dotnet build ReWASD.sln     # works on Windows and macOS
dotnet test ReWASD.sln
```

### What it can do

Remap a controller, keyboard or mouse to keys, mouse buttons, scrolling, macros, scripts and a virtual controller (Windows); sticks that
drive the mouse, wheel or keys; chords and sequences; shift layers; a command wheel; profiles that follow the app in front; import,
export and history; a full-screen controller tester (circle test, trigger pressure, timing, bounce, reaction test); a stream overlay; a lock; usage counts; macro steps that open pages, start apps, run commands, call webhooks and
control OBS. See [docs/features.md](docs/features.md) for all of it, and what is not included.

### Project Structure
- `.agents/skills/reverse-engineer-anything/` — REA workflow instructions and references for agent assistants.
- `src/` — `ReWASD.Core` (models), `ReWASD.Engine` (remapping, input, platform backends), `ReWASD.App` (shared view models), `ReWASD.UI` (WPF, Windows), `ReWASD.UI.Mac` (Avalonia, macOS).
- `GEMINI.md` — Project context and reverse-engineering rules.
