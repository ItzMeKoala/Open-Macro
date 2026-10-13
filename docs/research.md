# Research notes

ReWASD Open Source is a clean-room recreation. Its behaviour was worked out from documented evidence (how the commercial reWASD
structures remaps, activators and macros), and everything was then written again from scratch. No code, images or other assets were
copied.

- [rewasd-remap-macro-analysis.md](rewasd-remap-macro-analysis.md) records how remaps, activators, layers and macro steps are
  structured, and which of them are implemented here.
- [ui-structure.md](ui-structure.md) records how the original's editor is laid out (view names and nesting only) and how this
  project's layout maps to it.

## Tooling used for the research

The work was done with [REA (Reverse Engineer Anything)](https://github.com/morluto/rea), which inspects binaries, libraries,
virtual drivers and runtime behaviour through the Model Context Protocol, for agent assistants such as Antigravity. The project's
agent instructions are in `GEMINI.md` and the REA skill is in `.agents/skills/reverse-engineer-anything/`.

Prerequisites: Node.js 22 or later (24 LTS recommended) and the REA CLI, installed with `npm install --global rea-agents`. Check the
setup with:

```sh
rea doctor --client antigravity
```

To investigate a target, give the agent a binary (an installer, DLL, driver or executable) and ask, for example:

```text
Inspect the target binary [path/to/binary.dll] and explain how virtual gamepad bus communication (IOCTLs) and input remapping hooks are handled.
```

The agent calls REA's tools to decompile, map imports and exports, trace call trees and write up what it finds. Record findings,
offsets and signatures as evidence before implementing anything.

None of this is needed to build, run or contribute to the app.
