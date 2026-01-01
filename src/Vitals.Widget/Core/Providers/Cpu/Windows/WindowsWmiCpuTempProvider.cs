using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace Vitals.Widget.Core.Providers.Cpu;

/// <summary>
/// Best effort Windows CPU temperature via WMI.
/// Uses MSAcpi_ThermalZoneTemperature (no extra drivers).
/// Many systems expose only generic thermal zones, so this may return N/A.
/// </summary>
public sealed class WindowsWmiCpuTempProvider : ICpuTempProvider
{
    public bool TryGetCpuTempC(out int tempC)
    {
        tempC = 0;

        // This provider exists in the project on all OS's, but should never run off Windows.
        if (!OperatingSystem.IsWindows())
            return false;

        return TryGetCpuTempWindows(out tempC);
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetCpuTempWindows(out int tempC)
    {
        tempC = 0;

        try
        {
            var values = ReadTempsFromThermalZonesWindows();
            if (values.Count == 0)
                return false;

            var max = int.MinValue;
            foreach (var v in values)
            {
                if (v > max)
                    max = v;
            }

            if (max < 10 || max > 130)
                return false;

            tempC = max;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<int> ReadTempsFromThermalZonesWindows()
    {
        var temps = new List<int>();

        // MSAcpi_ThermalZoneTemperature lives in root\wmi
        using (var searcher = new ManagementObjectSearcher(
                   @"\\.\root\wmi",
                   "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject mo in results)
            {
                var rawObj = mo["CurrentTemperature"];
                if (rawObj == null)
                    continue;

                // Raw value is tenths of Kelvin (commonly UInt32)
                uint raw;
                try
                {
                    raw = Convert.ToUInt32(rawObj, CultureInfo.InvariantCulture);
                }
                catch
                {
                    continue;
                }

                // Convert to Celsius
                var c = (raw / 10.0) - 273.15;

                // Round to int for display
                var ci = (int)Math.Round(c);

                // Filter obvious garbage early
                if (ci > 0 && ci < 130)
                    temps.Add(ci);
            }
        }

        return temps;
    }

    public void Dispose()
    {
    }
}
