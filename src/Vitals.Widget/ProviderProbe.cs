using System;
using System.Collections.Generic;
using Vitals.Widget.Core.Providers;
using Vitals.Widget.Core.Providers.Cpu;
using Vitals.Widget.Core.Providers.Gpu;

namespace Vitals.Widget;

/// <summary>
/// "--probe" diagnostic mode: tries every provider for this OS individually,
/// prints what each one returns, then shows what ProviderManager would pick.
/// Debug/support tool; no UI.
/// </summary>
internal static class ProviderProbe
{
    public static void Run()
    {
        Console.WriteLine("VitalsWidget provider probe");
        Console.WriteLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitProcess ? "x64" : "x86")} process)");
        Console.WriteLine();

        var settings = WidgetSettingsStore.Load();
        Console.WriteLine($"UseLhmWmiBridge: {settings.UseLhmWmiBridge}");
        Console.WriteLine();

        Console.WriteLine("--- CPU providers ---");
        foreach (var (key, provider) in AllCpuProviders())
        {
            using (provider)
            {
                ProbeOne(key, () => provider.TryGetCpuTempC(out var t) ? t : (int?)null);
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- GPU providers ---");
        foreach (var (key, provider) in AllGpuProviders())
        {
            using (provider)
            {
                ProbeOne(key, () => provider.TryGetGpuTempC(out var t) ? t : (int?)null);
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- ProviderManager (what the widget will use) ---");
        using var manager = new ProviderManager(settings);
        Console.WriteLine(manager.TryGetCpuTempC(out var cpu) ? $"CPU: {cpu} C" : "CPU: N/A");
        Console.WriteLine(manager.TryGetGpuTempC(out var gpu) ? $"GPU: {gpu} C" : "GPU: N/A");
    }

    private static void ProbeOne(string key, Func<int?> read)
    {
        try
        {
            var started = DateTime.UtcNow;
            var temp = read();
            var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;

            Console.WriteLine(temp.HasValue
                ? $"{key,-24} OK    {temp.Value} C ({ms} ms)"
                : $"{key,-24} no reading ({ms} ms)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{key,-24} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static IEnumerable<(string, ICpuTempProvider)> AllCpuProviders()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return ("windows-cpu-wmi", new WindowsWmiCpuTempProvider());
            yield return ("windows-cpu-lhm-wmi", new WindowsLhmWmiCpuProvider());
        }

        if (OperatingSystem.IsLinux())
        {
            yield return ("linux-cpu-hwmon", new LinuxCpuHwmonProvider());
        }
    }

    private static IEnumerable<(string, IGpuTempProvider)> AllGpuProviders()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return ("nvidia-nvml", new NvidiaNvmlGpuProvider());
            yield return ("amd-adlx", new AmdAdlxGpuProvider());
            yield return ("intel", new IntelGpuProvider());
            yield return ("windows-gpu-lhm-wmi", new WindowsLhmWmiGpuProvider());
        }

        if (OperatingSystem.IsLinux())
        {
            yield return ("linux-amd-hwmon", new LinuxAmdHwmonGpuProvider());
            yield return ("linux-nvidia-hwmon", new LinuxNvidiaHwmonGpuProvider());
            yield return ("linux-nvidia-smi", new LinuxNvidiaSmiGpuProvider());
        }
    }
}
