using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// Linux NVIDIA GPU temperature provider using nvidia-smi.
/// Why:
/// - On some Linux setups the NVIDIA driver does not expose a hwmon node, so hwmon-based providers return N/A.
/// - nvidia-smi is typically available when the proprietary NVIDIA driver is installed.
/// Tradeoff:
/// - Spawning a process is heavier than reading sysfs, but it's reliable and still low overhead at 1 Hz.
/// </summary>
public sealed class LinuxNvidiaSmiGpuProvider : IGpuTempProvider
{
    public bool TryGetGpuTempC(out int tempC)
    {
        tempC = 0;

        if (!OperatingSystem.IsLinux())
            return false;

        try
        {
            // Prefer a known path if present, otherwise rely on PATH.
            var exe = File.Exists("/usr/bin/nvidia-smi") ? "/usr/bin/nvidia-smi" : "nvidia-smi";

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null)
                return false;

            // Very small output, short timeout is fine.
            if (!p.WaitForExit(800))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            var output = (p.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(output))
                return false;

            // If multiple GPUs exist, nvidia-smi can output multiple lines; we pick the hottest plausible value.
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var best = int.MinValue;

            foreach (var line in lines)
            {
                if (!int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
                    continue;

                if (t < 0 || t > 150)
                    continue;

                if (t > best)
                    best = t;
            }

            if (best == int.MinValue)
                return false;

            tempC = best;
            return true;
        }
        catch
        {
            // Command missing / permission / driver not installed / etc.
            return false;
        }
    }

    public void Dispose()
    {
    }
}
