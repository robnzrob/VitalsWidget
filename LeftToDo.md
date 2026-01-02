# LeftToDo (GUI)

Run:
Windows:
dotnet run --project src\Vitals.Widget

Linux:
dotnet run --project src/Vitals.Widget


## VitalsWidget

Tiny always-on-top widget showing CPU and GPU temperatures (minimal, glanceable, low overhead).

Target platforms:
- Windows 10/11
- Linux Mint (later)

## Current status (working)

GUI:
- Compact always-on-top window, rounded background, no decorations
- Drag to move when unlocked
- Right-click menu:
  - Lock / Unlock position
  - Show CPU / Show GPU toggles (instant resize)
  - Background opacity controls (more solid / more transparent / reset)
  - Reset position
  - Exit
- Auto height sizing:
  - Shrinks/grows based on visible lines
  - If both CPU and GPU are hidden, shows a placeholder line (`Vitals`)
  - Extra fix for docked overlays (eg SidebarDiagnostics) so toggling lines doesn’t “jump” the window

Settings (persisted to JSON):
- Window position (X/Y)
- Locked state
- Background opacity
- ShowCpu / ShowGpu
- Provider order lists for CPU/GPU per OS

Sensor skeleton:
- Provider interfaces + ProviderManager (OS detection, ordered provider selection, provider caching)
- Windows GPU providers:
  - NVIDIA NVML (working)
  - AMD ADL best-effort (implemented, test when AMD card installed)
  - Intel WMI best-effort (implemented, test on Arc machine)
- Windows CPU provider:
  - WMI best-effort (implemented, may show N/A depending on hardware)
- Linux providers (best-effort, test later):
  - CPU hwmon
  - AMD GPU hwmon
  - NVIDIA GPU hwmon

Display:
- CPU/GPU show temp when available, otherwise `N/A`
- Simple green → amber → red scale when a real temperature exists
- `N/A` uses a neutral colour
- Text stays fully opaque (only background is translucent)

## Remaining for “GUI v1 complete”

1) Structure tidy-up
- Clean up provider folder layout so Windows vs Linux is obvious
  - Preferred: `Cpu/Windows`, `Cpu/Linux`, `Gpu/Windows`, `Gpu/Linux`
- Do a quick scan for naming consistency (files and classes)

2) Code quality pass
- Add short “why” comments to:
  - ProviderManager (why caching, why ordered keys)
  - Each provider (what it relies on, why it may return N/A)
  - The post-resize position reassert (docked overlay work-area behaviour)
- Keep the Problems panel clean (no warnings)

3) Packaging hooks (GUI scope)
Add README notes for publishing:
- Windows:
  - publish command
  - output folder: `dist/win-x64`
- Linux:
  - publish command
  - output folder: `dist/linux-x64`

## Nice-to-have (not required for v1)

- Context menu: “Reset to defaults” (writes default settings.json)
- Debug-only indicator of which CPU/GPU provider is currently active (useful for Linux testing)
