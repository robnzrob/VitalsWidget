using System;

namespace Vitals.Widget.Core.Providers;

/// <summary>
/// CPU temperature provider interface.
/// Why: same reason as GPU, keeps OS/native specifics out of the UI.
/// </summary>
public interface ICpuTempProvider : IDisposable
{
    bool TryGetCpuTempC(out int tempC);
}
