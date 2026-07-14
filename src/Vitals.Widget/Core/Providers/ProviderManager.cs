using System;
using System.Collections.Generic;
using Vitals.Widget.Core.Providers.Cpu;
using Vitals.Widget.Core.Providers.Gpu;

namespace Vitals.Widget.Core.Providers;

/// <summary>
/// Picks provider order based on OS and settings, probes until one works, then caches.
/// Unknown keys are ignored so you can add future providers without breaking older builds.
/// If nothing works we back off for a few seconds instead of re-probing every UI tick.
/// </summary>
public sealed class ProviderManager : IDisposable
{
    private readonly WidgetSettings _settings;

    private readonly List<(string Key, Func<ICpuTempProvider> Create)> _cpuProviderFactories;
    private readonly List<(string Key, Func<IGpuTempProvider> Create)> _gpuProviderFactories;

    private ICpuTempProvider? _activeCpuProvider;
    private IGpuTempProvider? _activeGpuProvider;
    private string? _activeCpuKey;
    private string? _activeGpuKey;

    // When no provider works, don't keep probing every UI tick.
    // Instead, back off for a few seconds, then try again.
    private DateTime _nextCpuProbeUtc = DateTime.MinValue;
    private DateTime _nextGpuProbeUtc = DateTime.MinValue;

    private static readonly TimeSpan ProbeBackoff = TimeSpan.FromSeconds(5);

    public ProviderManager(WidgetSettings settings)
    {
        _settings = settings;
        _cpuProviderFactories = BuildCpuProviderFactories(settings);
        _gpuProviderFactories = BuildGpuProviderFactories(settings);
    }

    // The LHM WMI bridge can be toggled in settings at runtime; we check the shared
    // settings object at probe time so no ProviderManager rebuild is needed.
    private static bool IsLhmKey(string key) =>
        key.IndexOf("lhm", StringComparison.OrdinalIgnoreCase) >= 0;

    private bool IsEnabled(string key) => _settings.UseLhmWmiBridge || !IsLhmKey(key);

    public bool TryGetCpuTempC(out int tempC)
    {
        // Active provider path first. If it fails (or got toggled off), drop it
        // and re-probe so we can fall through to other providers.
        if (_activeCpuProvider != null)
        {
            try
            {
                if (IsEnabled(_activeCpuKey ?? string.Empty) && _activeCpuProvider.TryGetCpuTempC(out tempC))
                    return true;
            }
            catch
            {
                // Treat exceptions as failure and re-probe.
            }

            try { _activeCpuProvider.Dispose(); } catch { }
            _activeCpuProvider = null;
            _activeCpuKey = null;
        }

        if (DateTime.UtcNow < _nextCpuProbeUtc)
        {
            tempC = 0;
            return false;
        }

        foreach (var (key, create) in _cpuProviderFactories)
        {
            if (!IsEnabled(key))
                continue;

            ICpuTempProvider? candidate = null;

            try
            {
                candidate = create();

                if (candidate.TryGetCpuTempC(out tempC))
                {
                    System.Diagnostics.Debug.WriteLine($"[CPU] active provider: {key}");
                    _activeCpuProvider = candidate;
                    _activeCpuKey = key;
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[CPU] no reading from: {key}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CPU] provider threw: {key} ({ex.GetType().Name})");
            }

            try { candidate?.Dispose(); } catch { }
        }

        // Nothing worked: back off before we try again.
        _nextCpuProbeUtc = DateTime.UtcNow.Add(ProbeBackoff);

        tempC = 0;
        return false;
    }

    public bool TryGetGpuTempC(out int tempC)
    {
        // Active provider path first. If it fails (or got toggled off), drop it
        // and re-probe so we can fall through to other providers.
        if (_activeGpuProvider != null)
        {
            try
            {
                if (IsEnabled(_activeGpuKey ?? string.Empty) && _activeGpuProvider.TryGetGpuTempC(out tempC))
                    return true;
            }
            catch
            {
                // Treat exceptions as failure and re-probe.
            }

            try { _activeGpuProvider.Dispose(); } catch { }
            _activeGpuProvider = null;
            _activeGpuKey = null;
        }

        if (DateTime.UtcNow < _nextGpuProbeUtc)
        {
            tempC = 0;
            return false;
        }

        foreach (var (key, create) in _gpuProviderFactories)
        {
            if (!IsEnabled(key))
                continue;

            IGpuTempProvider? candidate = null;

            try
            {
                candidate = create();

                if (candidate.TryGetGpuTempC(out tempC))
                {
                    // Only cache a provider after it proves it can return a value.
                    System.Diagnostics.Debug.WriteLine($"[GPU] active provider: {key}");
                    _activeGpuProvider = candidate;
                    _activeGpuKey = key;
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[GPU] no reading from: {key}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GPU] provider threw: {key} ({ex.GetType().Name})");
            }

            try { candidate?.Dispose(); } catch { }
        }

        // Nothing worked: back off before we try again.
        _nextGpuProbeUtc = DateTime.UtcNow.Add(ProbeBackoff);

        tempC = 0;
        return false;
    }

    private static List<(string, Func<ICpuTempProvider>)> BuildCpuProviderFactories(WidgetSettings settings)
    {
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

            // Optional bridge: reads sensors LibreHardwareMonitor publishes to WMI
            // when the user runs it themselves. We ship no third-party code.
            // Registered always; the UseLhmWmiBridge toggle is checked at probe time.
            map["windows-cpu-lhm-wmi"] = () => new WindowsLhmWmiCpuProvider();
        }

        return ResolveOrder(order, map);
    }

    private static List<(string, Func<IGpuTempProvider>)> BuildGpuProviderFactories(WidgetSettings settings)
    {
        var order = OperatingSystem.IsLinux()
            ? (settings.GpuProviderOrderLinux ?? Array.Empty<string>())
            : (settings.GpuProviderOrderWindows ?? Array.Empty<string>());

        var map = new Dictionary<string, Func<IGpuTempProvider>>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            map["nvidia-nvml"] = () => new NvidiaNvmlGpuProvider();
            map["amd-adlx"] = () => new AmdAdlxGpuProvider();
            map["intel"] = () => new IntelGpuProvider();
            map["windows-gpu-lhm-wmi"] = () => new WindowsLhmWmiGpuProvider();
        }

        if (OperatingSystem.IsLinux())
        {
            map["linux-nvidia-hwmon"] = () => new LinuxNvidiaHwmonGpuProvider();
            map["linux-nvidia-smi"] = () => new LinuxNvidiaSmiGpuProvider();
            map["linux-amd-hwmon"] = () => new LinuxAmdHwmonGpuProvider();
        }

        return ResolveOrder(order, map);
    }

    private static List<(string, Func<T>)> ResolveOrder<T>(string[] order, Dictionary<string, Func<T>> map)
    {
        var list = new List<(string, Func<T>)>();

        foreach (var key in order)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (map.TryGetValue(key, out var factory))
                list.Add((key, factory));
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
