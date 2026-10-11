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

### Running it

Pull the latest and run it; the scripts rebuild first and close any older copy, so you always see the newest build.

- **Windows** (needs the .NET 9 SDK): `git pull` then `.\scripts\run-windows.ps1`. A virtual controller needs the ViGEmBus driver; hiding the pad needs HidHide.
- **macOS** (needs the .NET 9 SDK): `git pull && scripts/run-macos.sh`. See [docs/macos.md](docs/macos.md).

The bottom bar of the Mac app shows the version and git commit it was built from, so you can tell whether you are looking at the latest build.

```sh
dotnet build ReWASD.sln     # works on Windows and macOS
dotnet test ReWASD.sln
```

### Project Structure
- `.agents/skills/reverse-engineer-anything/` — REA workflow instructions and references for agent assistants.
- `src/` — `ReWASD.Core` (models), `ReWASD.Engine` (remapping, input, platform backends), `ReWASD.App` (shared view models), `ReWASD.UI` (WPF, Windows), `ReWASD.UI.Mac` (Avalonia, macOS).
- `GEMINI.md` — Project context and reverse-engineering rules.
