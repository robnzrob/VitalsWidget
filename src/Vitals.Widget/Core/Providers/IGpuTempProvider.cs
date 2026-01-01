using System;

namespace Vitals.Widget.Core.Providers;

/// <summary>
/// GPU temperature provider interface.
/// Why: keeps sensor implementation details out of the UI and lets us avoid loading
/// Windows only native libraries (like NVML) on Linux.
/// </summary>
public interface IGpuTempProvider : IDisposable
{
    bool TryGetGpuTempC(out int tempC);
}
