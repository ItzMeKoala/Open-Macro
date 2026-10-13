# Open-Macro Project Guidelines

## Project Purpose
This project focuses on analyzing, reverse-engineering, and cleanly reimplementing gamepad remapping, virtual device emulation, and macro/input features inspired by reWASD, using documented evidence and clean-room implementation practices.

## REA (Reverse Engineer Anything) Integration
REA is configured for this project to inspect binaries, libraries, virtual drivers, UI components, and runtime behavior.

### Tools & Capabilities
REA provides 170+ specialized analysis tools through the Model Context Protocol (MCP) and CLI:
- **PE & Native Analysis**: Inspecting Windows PE binaries (`.exe`, `.dll`, `.sys`), headers, exported functions, imported symbols, and string tables.
- **PE Resources**: `inspect_pe_resources` to parse manifest, icons, dialogs, and embedded configurations.
- **Managed & .NET Analysis**: `inspect_managed_artifact` for .NET assemblies and CIL bytecode.
- **JavaScript / Electron UI**: `analyze_javascript_application` for Electron frontend assets or ASAR bundles.
- **Function Analysis & Xrefs**: Inspecting procedure cross-references (`xrefs`), tracing execution paths, and identifying virtual gamepad API hooks.

### REA Workflow & Skill
- The `reverse-engineer-anything` skill is installed both in global Antigravity configuration (`~/.gemini/config/skills/`) and within the project (`.agents/skills/reverse-engineer-anything/`).
- Follow the evidence-driven workflow:
  1. **Identify the target**: Point to target executables, DLLs, or driver packages.
  2. **Summary first**: Run summary-level analysis before requesting complete disassembled functions or trace graphs.
  3. **Preserve evidence**: Document findings, offsets, API signatures, and IOCTL codes clearly.
  4. **Clean-room implementation**: Reconstruct behavior in open-source code (e.g. C++, C#, Rust, Python) based on verified functional requirements.

### Command-Line Usage
REA CLI is globally accessible via `rea`:
```powershell
# Verify environment and integrations
rea doctor --client antigravity

# Inspect PE resources of a target file
rea inspect-pe-resources path\to\target.exe

# Analyze JS/Electron components if applicable
rea analyze-javascript-application path\to\app --json
```
