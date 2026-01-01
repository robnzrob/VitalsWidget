using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Vitals.Widget.Core.Providers.Cpu;

/// <summary>
/// Linux CPU temp via /sys/class/hwmon.
/// Why: gives us a no-driver baseline on Linux. If it can't find a sane sensor, it returns false.
/// </summary>
public sealed class LinuxCpuHwmonProvider : ICpuTempProvider
{
    private readonly string? _tempInputPath;

    public LinuxCpuHwmonProvider()
    {
        _tempInputPath = TryFindCpuTempInputPath();
    }

    public bool TryGetCpuTempC(out int tempC)
    {
        tempC = 0;

        if (string.IsNullOrWhiteSpace(_tempInputPath))
            return false;

        try
        {
            var raw = File.ReadAllText(_tempInputPath).Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliC))
                return false;

            // hwmon commonly reports millidegrees C
            tempC = milliC >= 1000 ? (milliC / 1000) : milliC;
            return tempC > 0 && tempC < 130;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryFindCpuTempInputPath()
    {
        var root = "/sys/class/hwmon";
        if (!Directory.Exists(root))
            return null;

        try
        {
            var hwmons = Directory.GetDirectories(root, "hwmon*");

            // Prefer typical CPU sensor drivers
            string[] preferredNames = { "coretemp", "k10temp", "cpu" };

            foreach (var name in preferredNames)
            {
                var match = hwmons.FirstOrDefault(d =>
                {
                    var n = ReadHwmonName(d);
                    return n != null && n.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
                });

                var candidate = match != null ? FindFirstTempInput(match) : null;
                if (candidate != null)
                    return candidate;
            }

            // Fallback: any temp input at all
            foreach (var d in hwmons)
            {
                var candidate = FindFirstTempInput(d);
                if (candidate != null)
                    return candidate;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ReadHwmonName(string hwmonDir)
    {
        try
        {
            var path = Path.Combine(hwmonDir, "name");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindFirstTempInput(string hwmonDir)
    {
        try
        {
            var files = Directory.GetFiles(hwmonDir, "temp*_input");
            return files.Length > 0 ? files[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
    }
}
