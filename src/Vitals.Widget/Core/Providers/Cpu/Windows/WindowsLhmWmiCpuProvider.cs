using System;

namespace Vitals.Widget.Core.Providers.Cpu;

/// <summary>
/// Windows CPU temperature via the WMI sensors LibreHardwareMonitor publishes
/// while it is running (root\LibreHardwareMonitor). See LhmWmiSensorReader for why.
/// If LHM isn't running this returns false and ProviderManager falls through.
/// </summary>
public sealed class WindowsLhmWmiCpuProvider : ICpuTempProvider
{
    public bool TryGetCpuTempC(out int tempC)
    {
        tempC = 0;

        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // LHM CPU parents look like /amdcpu/0 or /intelcpu/0.
            var sensors = LhmWmiSensorReader.ReadTemperatureSensors("cpu");
            if (sensors.Count == 0)
                return false;

            // Prefer the whole-package readings, else fall back to the hottest core.
            double? best = null;

            foreach (var s in sensors)
            {
                if (LhmWmiSensorReader.NameContainsAny(s.Name, "Package", "Tctl", "Tdie", "Core Average"))
                {
                    best = s.ValueC;
                    break;
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
