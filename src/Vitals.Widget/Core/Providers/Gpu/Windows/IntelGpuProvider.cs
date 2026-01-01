using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// Windows Intel GPU "best effort" temperature provider using WMI thermal zones.
/// Why:
/// - Uses Windows built-in WMI (no extra installs).
/// - Cleanly returns false if the machine/driver doesn't expose a useful thermal zone.
/// Notes:
/// - Many systems do NOT expose discrete GPU temperature via WMI thermal zones.
/// - If it works, great. If not, it fails safely and ProviderManager falls through.
/// </summary>
public sealed class IntelGpuProvider : IGpuTempProvider
{
    private bool _initAttempted;
    private string? _cachedThermalZoneInstanceName;

    public bool TryGetGpuTempC(out int tempC)
    {
        tempC = 0;

        if (!OperatingSystem.IsWindows())
            return false;

        return TryGetGpuTempWindows(out tempC);
    }

    public void Dispose()
    {
    }

    [SupportedOSPlatform("windows")]
    private bool TryGetGpuTempWindows(out int tempC)
    {
        tempC = 0;

        try
        {
            // Cache a likely GPU thermal zone name on first successful discovery
            if (!_initAttempted)
            {
                _initAttempted = true;
                _cachedThermalZoneInstanceName = TryPickBestGpuThermalZoneInstanceName();
            }

            // If we have a cached zone, query that one only (lighter than scanning all each tick)
            if (!string.IsNullOrWhiteSpace(_cachedThermalZoneInstanceName))
            {
                if (TryReadThermalZoneTempC(_cachedThermalZoneInstanceName!, out tempC))
                    return IsPlausibleTemp(tempC);

                // If the cached one stopped working, drop it and try to re-discover next tick
                _cachedThermalZoneInstanceName = null;
                return false;
            }

            // Fallback: scan all zones and pick the best reading that looks GPU-ish
            var zones = ReadAllThermalZones();
            if (zones.Count == 0)
                return false;

            var best = FindBestGpuCandidate(zones);
            if (best.tempC == null)
                return false;

            tempC = best.tempC.Value;
            return IsPlausibleTemp(tempC);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? TryPickBestGpuThermalZoneInstanceName()
    {
        var zones = ReadAllThermalZones();
        if (zones.Count == 0)
            return null;

        // Prefer zones whose instance name looks GPU-related
        var bestScore = int.MinValue;
        string? bestName = null;

        foreach (var z in zones)
        {
            var score = ScoreThermalZoneName(z.instanceName);

            // If we got a GPU-ish name, take the highest score even if temp is missing right now
            if (score > bestScore)
            {
                bestScore = score;
                bestName = z.instanceName;
            }
        }

        // If we couldn't find anything that even smells like GPU, don't lock onto a random zone.
        // Returning null means we show N/A and let other providers try (or fall back).
        if (bestScore < 1)
            return null;

        return bestName;
    }

    [SupportedOSPlatform("windows")]
    private static bool TryReadThermalZoneTempC(string instanceName, out int tempC)
    {
        tempC = 0;

        // Query only the cached instance
        // InstanceName often contains backslashes, so we escape for WMI string literal.
        var escaped = instanceName.Replace("\\", "\\\\").Replace("'", "''");

        using (var searcher = new ManagementObjectSearcher(
                   @"\\.\root\wmi",
                   $"SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature WHERE InstanceName='{escaped}'"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject mo in results)
            {
                var rawObj = mo["CurrentTemperature"];
                if (rawObj == null)
                    continue;

                uint raw;
                try
                {
                    raw = Convert.ToUInt32(rawObj, CultureInfo.InvariantCulture);
                }
                catch
                {
                    continue;
                }

                // Tenths of Kelvin → Celsius
                var c = (raw / 10.0) - 273.15;
                tempC = (int)Math.Round(c);

                return true;
            }
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static List<(string instanceName, int? tempC)> ReadAllThermalZones()
    {
        var list = new List<(string instanceName, int? tempC)>();

        using (var searcher = new ManagementObjectSearcher(
                   @"\\.\root\wmi",
                   "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject mo in results)
            {
                var nameObj = mo["InstanceName"];
                var tempObj = mo["CurrentTemperature"];

                var name = nameObj as string;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                int? tempC = null;

                if (tempObj != null)
                {
                    try
                    {
                        var raw = Convert.ToUInt32(tempObj, CultureInfo.InvariantCulture);
                        var c = (raw / 10.0) - 273.15;
                        var ci = (int)Math.Round(c);

                        if (ci > 0 && ci < 150)
                            tempC = ci;
                    }
                    catch
                    {
                        // ignore bad sensor values
                    }
                }

                list.Add((name, tempC));
            }
        }

        return list;
    }
    private static (string instanceName, int? tempC) FindBestGpuCandidate(List<(string instanceName, int? tempC)> zones)
    {
        var bestScore = int.MinValue;

        // Initialize with a safe non-null instanceName to satisfy nullable analysis
        var best = (instanceName: string.Empty, tempC: (int?)null);

        foreach (var z in zones)
        {
            var score = ScoreThermalZoneName(z.instanceName);

            // Prefer GPU-ish names first, then prefer a real temperature.
            if (z.tempC != null)
                score += 2;

            if (score > bestScore)
            {
                bestScore = score;
                best = z;
            }
        }

        // If nothing looks GPU-ish or there's no usable temp, signal "not supported"
        if (bestScore < 1 || best.tempC == null)
            return (string.Empty, null);

        return best;
    }


    private static int ScoreThermalZoneName(string name)
    {
        // Very simple heuristics. Works only when the platform exposes meaningful InstanceName labels.
        // We keep this conservative to avoid accidentally using CPU/board zones.
        var n = name.ToLowerInvariant();

        var score = 0;

        if (n.Contains("gpu")) score += 5;
        if (n.Contains("gfx")) score += 4;
        if (n.Contains("igpu")) score += 3;
        if (n.Contains("dgpu")) score += 3;
        if (n.Contains("vga")) score += 2;
        if (n.Contains("video")) score += 2;
        if (n.Contains("intel")) score += 1;

        // Penalize common CPU-ish labels (so we don't steal the CPU zone)
        if (n.Contains("cpu")) score -= 4;
        if (n.Contains("core")) score -= 2;
        if (n.Contains("pch")) score -= 2;
        if (n.Contains("acpi")) score -= 1;

        return score;
    }

    private static bool IsPlausibleTemp(int tempC)
    {
        return tempC >= 0 && tempC <= 150;
    }
}
