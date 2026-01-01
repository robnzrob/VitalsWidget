# VitalsWidget

Tiny always-on-top widget showing CPU and GPU temperatures (minimal, glanceable, low overhead).

This is primarily built for personal use on:

- Windows 10/11
- Linux Mint (later)

## What it looks like (v1)

Two lines only:

- CPU 55°C
- GPU 45°C

Each line is color-coded by temperature (green -> amber -> red) so you can see issues instantly.

## Core rules (design constraints)

- No kernel drivers shipped or installed by this project
- Read-only telemetry only (no system changes)
- No auto-updater
- No telemetry / analytics / “phone home”
- Minimal UI (no wasted space)

## Features (v1 scope)

Widget

- Always on top
- Draggable
- Lock/unlock position
- Adjustable transparency
- Tray icon menu:
  - Show/hide
  - Lock/unlock
  - Opacity presets
  - Exit

Sensors (Windows v1)

- CPU temperature (best available source, otherwise show N/A)
- GPU temperature:
  - NVIDIA via NVML
  - AMD via ADLX

Sensors (Linux later)

- CPU temp via /sys and hwmon
- AMD GPU temp via /sys/class/drm and hwmon

## Project structure (planned)

- src/Vitals.Widget
  Avalonia UI app (stable, minimal changes long term)
- src/Vitals.Shared (later)
  Snapshot contract (DTOs) shared between UI and sensor host
- src/Vitals.Host.Windows (later)
  Sensor host for Windows
- src/Vitals.Host.Linux (later)
  Sensor host for Linux

Note: v1 can start with hard-coded test temperatures to finish the UI first, then wire real sensors.

## Build and run (Windows)

From repo root:

dotnet run --project src/Vitals.Widget

## Roadmap (pragmatic)

Phase 1

- Create the widget UI
- Hard-coded CPU/GPU temps to validate layout, colors, tray, lock, opacity

Phase 2

- Add real Windows sensors (NVIDIA NVML, AMD ADLX)
- Grac
