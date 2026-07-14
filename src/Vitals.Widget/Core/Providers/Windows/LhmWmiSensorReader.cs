using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace Vitals.Widget.Core.Providers;

/// <summary>
/// Reads temperature sensors that LibreHardwareMonitor (or OpenHardwareMonitor)
/// publishes to WMI while it is running.
/// Why this approach:
/// - We ship zero third-party code. If the user chooses to run LHM, we simply
///   read the sensors it already exposes via the OS (WMI), same as reading hwmon on Linux.
/// - If LHM isn't running, the WMI namespace doesn't exist and we cleanly return nothing.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class LhmWmiSensorReader
{
    // LibreHardwareMonitor publishes to root\LibreHardwareMonitor.
    // OpenHardwareMonitor (its ancestor) uses root\OpenHardwareMonitor with the same schema.
    private static readonly string[] Namespaces =
    {
        @"root\LibreHardwareMonitor",
        @"root\OpenHardwareMonitor"
    };

    public readonly record struct TempSensor(string Name, string Parent, double ValueC);

    /// <summary>
    /// Returns all temperature sensors whose parent hardware identifier contains
    /// any of the given fragments (e.g. "cpu" or "gpu"). Empty list if LHM isn't running.
    /// </summary>
    public static List<TempSensor> ReadTemperatureSensors(params string[] parentFragments)
    {
        var sensors = new List<TempSensor>();

        foreach (var ns in Namespaces)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    ns,
                    "SELECT Name, Value, Parent FROM Sensor WHERE SensorType='Temperature'");
                using var results = searcher.Get();

                foreach (ManagementObject mo in results)
                {
                    var name = mo["Name"] as string ?? string.Empty;
                    var parent = mo["Parent"] as string ?? string.Empty;
                    var valueObj = mo["Value"];

                    if (valueObj == null)
                        continue;

                    if (!MatchesAny(parent, parentFragments))
                        continue;

                    double value;
                    try
                    {
                        value = Convert.ToDouble(valueObj, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        continue;
                    }

                    // Filter obvious garbage early
                    if (value <= 0 || value > 150)
                        continue;

                    sensors.Add(new TempSensor(name, parent, value));
                }

                // If a namespace worked, don't also query the other one.
                if (sensors.Count > 0)
                    return sensors;
            }
            catch
            {
                // Namespace missing (LHM not running) or WMI unhappy. Try the next one.
            }
        }

        return sensors;
    }

    public static bool NameContainsAny(string name, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool MatchesAny(string parent, string[] fragments)
    {
        if (fragments.Length == 0)
            return true;

        foreach (var f in fragments)
        {
            if (parent.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
