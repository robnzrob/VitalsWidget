using System;
using System.Collections.Generic;
using Vitals.Widget.Core.Providers.Cpu;
using Vitals.Widget.Core.Providers.Gpu;

namespace Vitals.Widget.Core.Providers;

/// <summary>
/// Picks provider order based on OS and settings, probes until one works, then caches.
/// Unknown keys are ignored so you can add future providers without breaking older builds.
/// </summary>
public sealed class ProviderManager : IDisposable
{
    private readonly List<Func<ICpuTempProvider>> _cpuProviderFactories;
    private readonly List<Func<IGpuTempProvider>> _gpuProviderFactories;

    private ICpuTempProvider? _activeCpuProvider;
    private IGpuTempProvider? _activeGpuProvider;

    public ProviderManager(WidgetSettings settings)
    {
        _cpuProviderFactories = BuildCpuProviderFactories(settings);
        _gpuProviderFactories = BuildGpuProviderFactories(settings);
    }

    public bool TryGetCpuTempC(out int tempC)
    {
        if (_activeCpuProvider != null)
            return _activeCpuProvider.TryGetCpuTempC(out tempC);

        foreach (var factory in _cpuProviderFactories)
        {
            ICpuTempProvider? candidate = null;

            try
            {
                candidate = factory();

                if (candidate.TryGetCpuTempC(out tempC))
                {
                    _activeCpuProvider = candidate;
                    return true;
                }
            }
            catch
            {
            }

            try { candidate?.Dispose(); } catch { }
        }

        tempC = 0;
        return false;
    }

    public bool TryGetGpuTempC(out int tempC)
    {
        // If we have an active provider, use it first.
        // If it fails, drop it and re-probe so we can fall through to other providers.
        if (_activeGpuProvider != null)
        {
            try
            {
                if (_activeGpuProvider.TryGetGpuTempC(out tempC))
                    return true;
            }
            catch
            {
                // Treat exceptions as a failure and re-probe.
            }

            try { _activeGpuProvider.Dispose(); } catch { }
            _activeGpuProvider = null;
        }

        foreach (var factory in _gpuProviderFactories)
        {
            IGpuTempProvider? candidate = null;

            try
            {
                candidate = factory();

                if (candidate.TryGetGpuTempC(out tempC))
                {
                    // Only cache a provider after it proves it can return a value.
                    _activeGpuProvider = candidate;
                    return true;
                }
            }
            catch
            {
                // Ignore provider failures and try the next.
            }

            try { candidate?.Dispose(); } catch { }
        }

        tempC = 0;
        return false;
    }


    private static List<Func<ICpuTempProvider>> BuildCpuProviderFactories(WidgetSettings settings)
    {
        var list = new List<Func<ICpuTempProvider>>();

        var order = OperatingSystem.IsLinux()
            ? (settings.CpuProviderOrderLinux ?? Array.Empty<string>())
            : (settings.CpuProviderOrderWindows ?? Array.Empty<string>());

        var map = new Dictionary<string, Func<ICpuTempProvider>>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsLinux())
        {
            map["linux-cpu-hwmon"] = () => new LinuxCpuHwmonProvider();
        }

        if (OperatingSystem.IsWindows())
        {
            map["windows-cpu-wmi"] = () => new WindowsWmiCpuTempProvider();
        }

        // Windows CPU providers intentionally empty for now

        foreach (var key in order)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            Func<ICpuTempProvider>? factory;
            if (map.TryGetValue(key, out factory))
                list.Add(factory);
        }

        return list;
    }

    private static List<Func<IGpuTempProvider>> BuildGpuProviderFactories(WidgetSettings settings)
    {
        var list = new List<Func<IGpuTempProvider>>();

        var order = OperatingSystem.IsLinux()
            ? (settings.GpuProviderOrderLinux ?? Array.Empty<string>())
            : (settings.GpuProviderOrderWindows ?? Array.Empty<string>());

        var map = new Dictionary<string, Func<IGpuTempProvider>>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            map["nvidia-nvml"] = () => new NvidiaNvmlGpuProvider();

            // Skeleton providers for future work
            map["amd-adlx"] = () => new AmdAdlxGpuProvider();
            map["intel"] = () => new IntelGpuProvider();
        }

        if (OperatingSystem.IsLinux())
        {
            map["linux-nvidia-hwmon"] = () => new LinuxNvidiaHwmonGpuProvider();
            map["linux-nvidia-smi"] = () => new LinuxNvidiaSmiGpuProvider();
            map["linux-amd-hwmon"] = () => new LinuxAmdHwmonGpuProvider();
        }

        foreach (var key in order)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            Func<IGpuTempProvider>? factory;
            if (map.TryGetValue(key, out factory))
                list.Add(factory);
        }

        return list;
    }

    public void Dispose()
    {
        try { _activeCpuProvider?.Dispose(); } catch { }
        try { _activeGpuProvider?.Dispose(); } catch { }

        _activeCpuProvider = null;
        _activeGpuProvider = null;
    }
}
