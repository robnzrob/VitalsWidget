using System;
using Vitals.Widget.Sensors;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// NVML based NVIDIA GPU temperature provider (Windows only for v1).
/// Why: isolates all NVML usage here so the UI never directly calls NVML.
/// </summary>
public sealed class NvidiaNvmlGpuProvider : IGpuTempProvider
{
    public bool TryGetGpuTempC(out int tempC)
    {
        // This call is the only place we touch the NVML reader.
        // If NVML is not available, the reader should return false or throw;
        // either way ProviderManager will treat it as "not available".
        return NvidiaNvmlGpuReader.TryGetGpuTempC(out tempC);
    }

    public void Dispose()
    {
        // Be defensive. Shutdown should be safe to call even if NVML never started.
        try
        {
            NvidiaNvmlGpuReader.Shutdown();
        }
        catch
        {
            // Ignore shutdown failures. This is a best effort cleanup path.
        }
    }
}
