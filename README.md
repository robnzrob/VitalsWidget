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
- Lock / unlock position (Added Lockspot for when widget is locked in the bottom left corner, only visible when hovered over)
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
No drivers shipped or installed by this project, no third-party libraries.

Tested
- NVIDIA GPU (Windows): NVML via the installed driver, tested on RTX 3070
- AMD GPU (Windows): ADL Overdrive8 PMLog via the installed driver (atiadlxx.dll), tested on RX 9070 XT
  (older cards fall back to OverdriveN / Overdrive5 automatically)

Implemented, not fully tested yet
- Intel GPU provider (untested)
- Windows CPU via WMI (basic, may show N/A on some systems)
- Linux CPU via hwmon

Windows CPU temperature honesty note:
Windows has no built-in, driver-free API for CPU core temperatures.
Tools that show them all load a kernel driver. This project won't ship one.
Instead, if you run LibreHardwareMonitor yourself, the widget reads the sensors
LHM publishes to WMI (root\LibreHardwareMonitor). This is a passive, local,
read-only query - no ports, no network, and no LHM code included in this project.

To show CPU temperature on Windows:
1. Download LibreHardwareMonitor v0.9.4:
   https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.4
   (must be v0.9.4 or earlier - the WMI provider was removed in v0.9.5)
2. Run LibreHardwareMonitor.exe (it asks for admin - that's what reading
   CPU sensors requires, and exactly why this project won't do it itself)
3. In LHM's Options menu tick: Run On Windows Startup, Minimize To Tray,
   Start Minimized
4. In VitalsWidget Settings, leave "Sensor bridge" ticked (default on)

CPU temperature appears within ~5 seconds whenever LHM is running.
Don't want it? Untick "Sensor bridge" and the widget touches nothing.

If a sensor can’t be read, the widget shows `N/A`.

Diagnostics: run `Vitals.Widget.exe --probe` from a script or with output
redirected to a file to see what every provider returns on your machine.

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
