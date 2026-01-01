using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// Linux NVIDIA GPU temp via /sys/class/hwmon (when exposed by the driver).
/// Best effort. Returns false if not found.
/// </summary>
public sealed class LinuxNvidiaHwmonGpuProvider : IGpuTempProvider
{
    private readonly string? _tempInputPath;

    public LinuxNvidiaHwmonGpuProvider()
    {
        _tempInputPath = TryFindGpuTempInputPath("nvidia");
    }

    public bool TryGetGpuTempC(out int tempC)
    {
        tempC = 0;

        if (string.IsNullOrWhiteSpace(_tempInputPath))
            return false;

        try
        {
            var raw = File.ReadAllText(_tempInputPath).Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliC))
                return false;

            tempC = milliC >= 1000 ? (milliC / 1000) : milliC;
            return tempC > 0 && tempC < 130;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryFindGpuTempInputPath(string nameContains)
    {
        var root = "/sys/class/hwmon";
        if (!Directory.Exists(root))
            return null;

        try
        {
            var hwmons = Directory.GetDirectories(root, "hwmon*");

            var match = hwmons.FirstOrDefault(d =>
            {
                var n = ReadHwmonName(d);
                return n != null && n.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
            });

            if (match == null)
                return null;

            var temp1 = Path.Combine(match, "temp1_input");
            if (File.Exists(temp1))
                return temp1;

            var any = Directory.GetFiles(match, "temp*_input");
            return any.Length > 0 ? any[0] : null;
        }
        catch
        {
            return null;
        }
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

    public void Dispose()
    {
    }
}
