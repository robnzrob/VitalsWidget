using System;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// Windows GPU temperature via the WMI sensors LibreHardwareMonitor publishes
/// while it is running (root\LibreHardwareMonitor). See LhmWmiSensorReader for why.
/// Intended as a last-resort fallback after the native driver providers.
/// </summary>
public sealed class WindowsLhmWmiGpuProvider : IGpuTempProvider
{
    public bool TryGetGpuTempC(out int tempC)
    {
        tempC = 0;

        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // LHM GPU parents look like /gpu-amd/0, /gpu-nvidia/0, /gpu-intel/0
            // (OpenHardwareMonitor used /atigpu/0 and /nvidiagpu/0).
            var sensors = LhmWmiSensorReader.ReadTemperatureSensors("gpu");
            if (sensors.Count == 0)
                return false;

            // Prefer edge/core readings, then hotspot/junction, else the hottest sensor.
            double? best = null;

            foreach (var s in sensors)
            {
                if (LhmWmiSensorReader.NameContainsAny(s.Name, "GPU Core", "Edge"))
                {
                    best = s.ValueC;
                    break;
                }
            }

            if (best == null)
            {
                foreach (var s in sensors)
                {
                    if (LhmWmiSensorReader.NameContainsAny(s.Name, "Hot Spot", "Hotspot", "Junction"))
                    {
                        best = s.ValueC;
                        break;
                    }
                }
            }

            if (best == null)
            {
                var max = double.MinValue;
                foreach (var s in sensors)
                {
                    if (s.ValueC > max)
                        max = s.ValueC;
                }

                best = max;
            }

            var t = (int)Math.Round(best.Value);
            if (t < 0 || t > 150)
                return false;

            tempC = t;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
