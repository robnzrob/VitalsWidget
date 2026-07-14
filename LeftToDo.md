# LeftToDo (GUI)

Run:
Windows:
dotnet run --project src\Vitals.Widget

Linux:
dotnet run --project src/Vitals.Widget

To Publish (WIndows)
dotnet publish .\src\Vitals.Widget\Vitals.Widget.csproj -c Release -r win-x64 --self-contained false -o "C:\Work\VitalsWidget\publish\win-x64" -v minimal

to publish Linux
cd /home/robert/Work/VitalsWidget

dotnet publish ./src/Vitals.Widget/Vitals.Widget.csproj \
  -c Release -r linux-x64 --self-contained false \
  -o ./publish/linux-x64

chmod +x ./publish/linux-x64/Vitals.Widget

mkdir -p /home/robert/Apps/VitalsWidget
cp -a ./publish/linux-x64/. /home/robert/Apps/VitalsWidget/

chmod +x /home/robert/Apps/VitalsWidget/Vitals.Widget


## Current status

Working
- Always-on-top widget
- Drag when unlocked
- Lock/unlock position
- Show CPU / Show GPU toggles
- Settings window (font size, width, opacity, units, labels)
- Position persistence
- Edge positioning is less strict (keeps a tiny part visible so you can always grab it)
- Added Lockspot for when widget is locked in the bottom left corner, only visible when hovered over
Sensors
- NVIDIA GPU working (tested on RTX 3070)
- AMD GPU working on Windows via ADL Overdrive8 PMLog (tested on RX 9070 XT)
- Windows CPU WMI provider implemented (may show N/A depending on hardware)
- Optional LibreHardwareMonitor WMI bridge working (tested: CPU Tctl/Tdie on
  Ryzen + X570). Needs LHM v0.9.4 or earlier - WMI provider was removed in 0.9.5.
  Toggle in Settings; no LHM code shipped.
- Linux hwmon providers implemented (test coverage varies by hardware/driver)
- `--probe` diagnostic mode prints what every provider returns (redirect stdout)

## Next tests

- AMD GPU Linux provider test (RX 9070 XT)
- Intel GPU test (Arc machine if possible)
- Confirm behaviour on multi-monitor setups and different Linux desktops

## Release packaging

- Create dist output folders
  - dist/win-x64
  - dist/linux-x64
- Produce archives
  - dist/VitalsWidget-win-x64.zip
  - dist/VitalsWidget-linux-x64.tar.gz
- Generate SHA256 hashes for release artifacts
- Add GitHub Release notes template

## Docs

- Update README with:
  - tested hardware list
  - clear install steps
  - autostart steps
  - where settings.json is stored
  - “no telemetry / no network” statement
- Add screenshots (Windows and Linux)

## Nice to have

- Display which provider is active in the UI (probe mode covers the debug case)
- Optional “dock to corner” mode (top-right etc) with configurable margin

