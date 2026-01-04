# VitalsWidget

Tiny always-on-top widget showing CPU and GPU temperatures.
Minimal, glanceable, no nonsense.

## Why it exists

On Linux I couldn’t find a truly simple “CPU/GPU temp overlay” that behaves like a tiny widget.
I wanted something I can park on a side monitor and trust at a glance, similar to having a temp display on a GPU.

## Features

Widget
- Always on top
- Transparent window with subtle background
- Drag to move (when unlocked)
- Lock / unlock position
- Show CPU and Show GPU toggles
- Settings window:
  - Font size
  - Width
  - Background opacity
  - Celsius / Fahrenheit
  - Optional units suffix (°C / °F)
  - Optional labels (CPU / GPU)

Behaviour
- Remembers position and settings
- Supports being positioned very close to the edge of the screen
- Color thresholds (simple and fast to read):
  - <= 75: normal
  - 76–89: warning
  - >= 90: hot

## Platforms

- Windows 10/11
- Linux (tested on Linux Mint Cinnamon X11)

## Sensors and providers

Goal: use existing system interfaces only.
No drivers shipped or installed by this project.

Tested
- NVIDIA GPU: tested on RTX 3070

Implemented, not fully tested yet
- AMD GPU provider (planned test on RX 7900 XT)
- Intel GPU provider (untested)
- Windows CPU via WMI (basic, may show N/A on some systems)
- Linux CPU via hwmon

If a sensor can’t be read, the widget shows `N/A`.

## Install

VitalsWidget is portable. No installer.

Windows
1) Download the zip from GitHub Releases
2) Extract anywhere
3) Run `Vitals.Widget.exe`

Linux
1) Download the tar.gz from GitHub Releases
2) Extract anywhere
3) Run `./Vitals.Widget`

## Autostart

Linux (simple)
Use your desktop’s Startup Applications and point it at the `Vitals.Widget` binary.

Linux (manual autostart)
Create a file at:
`~/.config/autostart/vitalswidget.desktop`

Example:
```ini
[Desktop Entry]
Type=Application
Name=VitalsWidget
Exec=/home/youruser/Apps/VitalsWidget/Vitals.Widget
X-GNOME-Autostart-enabled=true
