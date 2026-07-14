using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Vitals.Widget.Core.Providers.Gpu;

/// <summary>
/// Windows AMD GPU temperature provider using AMD ADL (atiadlxx.dll).
/// Why this approach:
/// - Uses the installed AMD driver stack (you don't ship drivers).
/// - Fits the "try providers in order, cache winner" skeleton.
/// - If ADL is missing/not supported, we just return false and let ProviderManager fall through.
/// </summary>
public sealed class AmdAdlxGpuProvider : IGpuTempProvider
{
    private bool _initAttempted;
    private AdlSession? _adl;

    public bool TryGetGpuTempC(out int tempC)
    {
        tempC = 0;

        // This provider is Windows-only. On Linux the ProviderManager should never select it,
        // but this guard keeps it harmless even if accidentally called.
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // Avoid re-probing every tick. ProviderManager already caches the active provider,
            // but if this provider is tried and fails, we also don't want to re-init endlessly.
            if (!_initAttempted)
            {
                _initAttempted = true;
                _adl = AdlSession.TryCreate();
            }

            return _adl != null && _adl.TryGetTemperature(out tempC);
        }
        catch
        {
            // Never let a sensor provider crash the UI loop.
            return false;
        }
    }

    public void Dispose()
    {
        // This provider should never be active off Windows, but guard anyway so analyzers are happy
        // and accidental calls remain harmless.
        if (!OperatingSystem.IsWindows())
        {
            _adl = null;
            return;
        }

        _adl?.Dispose();
        _adl = null;
    }


    [SupportedOSPlatform("windows")]
    private sealed class AdlSession : IDisposable
    {
        private const int ADL_OK = 0;

        // Native library + ADL context
        private IntPtr _lib;
        private IntPtr _context;

        // Keep the alloc delegate alive for the lifetime of the ADL session (ADL stores it)
        private AdlMainMallocCallback _mallocCallback = default!;

        // Function delegates (loaded dynamically so the app still runs when ADL is absent)
        private ADL2_Main_Control_Create _mainCreate = default!;
        private ADL2_Main_Control_Destroy _mainDestroy = default!;
        private ADL2_Adapter_NumberOfAdapters_Get _adapterCountGet = default!;
        private ADL2_New_QueryPMLogData_Get? _pmLogGet;                 // Overdrive8, needed on RDNA cards
        private ADL2_OverdriveN_Temperature_Get? _odnTempGet;           // older (pre-RDNA)
        private ADL2_Overdrive5_Temperature_Get? _od5TempGet;           // oldest fallback

        private int _adapterCount;

        private AdlSession()
        {
        }

        public static AdlSession? TryCreate()
        {
            // ADL is normally exposed via the AMD driver install.
            // We load dynamically so on non-AMD systems this provider just returns false.
            if (!NativeLibrary.TryLoad("atiadlxx.dll", out var lib) &&
                !NativeLibrary.TryLoad("atiadlxy.dll", out lib))
                return null;

            var s = new AdlSession
            {
                _lib = lib
            };

            try
            {
                // Required core entry points
                s._mainCreate = GetDelegate<ADL2_Main_Control_Create>(lib, "ADL2_Main_Control_Create");
                s._mainDestroy = GetDelegate<ADL2_Main_Control_Destroy>(lib, "ADL2_Main_Control_Destroy");
                s._adapterCountGet = GetDelegate<ADL2_Adapter_NumberOfAdapters_Get>(lib, "ADL2_Adapter_NumberOfAdapters_Get");

                // Optional temperature APIs (we'll try what exists).
                // PMLog (Overdrive8) is the only one modern RDNA drivers still serve;
                // OverdriveN/5 keep older cards working.
                s._pmLogGet = TryGetDelegate<ADL2_New_QueryPMLogData_Get>(lib, "ADL2_New_QueryPMLogData_Get");
                s._odnTempGet = TryGetDelegate<ADL2_OverdriveN_Temperature_Get>(lib, "ADL2_OverdriveN_Temperature_Get");
                s._od5TempGet = TryGetDelegate<ADL2_Overdrive5_Temperature_Get>(lib, "ADL2_Overdrive5_Temperature_Get");

                // ADL requires a malloc callback when creating a context.
                // We keep it simple: allocate unmanaged memory. For the calls we use here,
                // ADL won't typically allocate large buffers every tick.
                s._mallocCallback = AdlMalloc;

                var rc = s._mainCreate(s._mallocCallback, 1, out s._context);
                if (rc != ADL_OK || s._context == IntPtr.Zero)
                {
                    s.Dispose();
                    return null;
                }

                rc = s._adapterCountGet(s._context, out s._adapterCount);
                if (rc != ADL_OK || s._adapterCount <= 0)
                {
                    s.Dispose();
                    return null;
                }

                // If we don't have ANY temp method, there's nothing useful we can do.
                if (s._pmLogGet == null && s._odnTempGet == null && s._od5TempGet == null)
                {
                    s.Dispose();
                    return null;
                }

                return s;
            }
            catch
            {
                s.Dispose();
                return null;
            }
        }

        public bool TryGetTemperature(out int tempC)
        {
            tempC = 0;

            if (_context == IntPtr.Zero || _adapterCount <= 0)
                return false;

            // We try all adapters until one returns a sane temperature.
            // This avoids needing adapter vendor filtering and works fine for a single-GPU machine.
            for (var i = 0; i < _adapterCount; i++)
            {
                // 1) Prefer Overdrive8 PMLog. On RDNA cards (RX 5000+) this is the only
                //    ADL temperature API the driver still answers; ODN/OD5 return errors there.
                if (_pmLogGet != null && TryReadPmLogTempC(i, out var pmTemp))
                {
                    tempC = pmTemp;
                    return true;
                }

                // 2) Fallback to OverdriveN (pre-RDNA cards)
                if (_odnTempGet != null)
                {
                    var raw = 0;

                    // iTemperatureType: 0 is the usual "GPU temp" request in most ADL samples.
                    var rc = _odnTempGet(_context, i, 0, out raw);
                    if (rc == ADL_OK)
                    {
                        // Some ADL APIs return millidegrees, some return degrees.
                        // This heuristic keeps the provider resilient across driver/OD versions.
                        var t = raw > 1000 ? raw / 1000 : raw;

                        if (t >= 0 && t <= 150)
                        {
                            tempC = t;
                            return true;
                        }
                    }
                }

                // 3) Fallback to Overdrive5 (oldest cards)
                if (_od5TempGet != null)
                {
                    var t = new ADLTemperature
                    {
                        iSize = Marshal.SizeOf<ADLTemperature>(),
                        iTemperature = 0
                    };

                    var rc = _od5TempGet(_context, i, 0, ref t);
                    if (rc == ADL_OK)
                    {
                        var c = t.iTemperature / 1000; // ADLTemperature is in millidegrees
                        if (c >= 0 && c <= 150)
                        {
                            tempC = c;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryReadPmLogTempC(int adapterIndex, out int tempC)
        {
            tempC = 0;

            if (_pmLogGet == null)
                return false;

            var output = new ADLPMLogDataOutput
            {
                iSize = Marshal.SizeOf<ADLPMLogDataOutput>(),
                Data = new int[PmLogDataInts]
            };

            var rc = _pmLogGet(_context, adapterIndex, ref output);
            if (rc != ADL_OK || output.Data == null)
                return false;

            // Each sensor slot is a { supported, value } int pair.
            // Prefer edge temp (the classic "GPU temp"), then GFX die, then hotspot.
            foreach (var sensorId in PreferredPmLogTempSensors)
            {
                var supported = output.Data[sensorId * 2];
                var value = output.Data[sensorId * 2 + 1];

                if (supported == 0)
                    continue;

                // Defensive: PMLog temps are documented in degrees C, but keep the
                // same millidegree heuristic as the older APIs just in case.
                var t = value > 1000 ? value / 1000 : value;

                if (t > 0 && t <= 150)
                {
                    tempC = t;
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            try
            {
                if (_context != IntPtr.Zero)
                {
                    _mainDestroy(_context);
                    _context = IntPtr.Zero;
                }
            }
            catch
            {
                // swallow; shutdown should be best-effort
            }

            try
            {
                if (_lib != IntPtr.Zero)
                {
                    NativeLibrary.Free(_lib);
                    _lib = IntPtr.Zero;
                }
            }
            catch
            {
                // swallow; shutdown should be best-effort
            }
        }

        private static IntPtr AdlMalloc(int size)
        {
            // ADL uses this callback for internal allocations.
            // In our minimal usage pattern, this won't be hit frequently.
            return Marshal.AllocHGlobal(size);
        }

        private static T GetDelegate<T>(IntPtr lib, string name) where T : Delegate
        {
            var ptr = NativeLibrary.GetExport(lib, name);
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        private static T? TryGetDelegate<T>(IntPtr lib, string name) where T : Delegate
        {
            try
            {
                var ptr = NativeLibrary.GetExport(lib, name);
                return Marshal.GetDelegateForFunctionPointer<T>(ptr);
            }
            catch
            {
                return null;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AdlMainMallocCallback(int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_Main_Control_Create(AdlMainMallocCallback callback, int iEnumConnectedAdapters, out IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_Main_Control_Destroy(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_New_QueryPMLogData_Get(IntPtr context, int iAdapterIndex, ref ADLPMLogDataOutput lpDataOutput);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_OverdriveN_Temperature_Get(IntPtr context, int iAdapterIndex, int iTemperatureType, out int iTemperature);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ADL2_Overdrive5_Temperature_Get(IntPtr context, int iAdapterIndex, int iThermalControllerIndex, ref ADLTemperature lpTemperature);

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLTemperature
        {
            public int iSize;

            // Temperature in millidegrees Celsius (e.g. 42000 = 42°C)
            public int iTemperature;
        }

        // --- Overdrive8 PMLog (from adl_defines.h / adl_structures.h) ---

        // ADL_PMLOG_MAX_SENSORS = 256 slots of ADLSingleSensorData { int supported; int value; }
        private const int PmLogMaxSensors = 256;
        private const int PmLogDataInts = PmLogMaxSensors * 2;

        // ADL_PMLOG_SENSORS ids we care about (order = preference)
        private const int PmLogTemperatureEdge = 8;      // ADL_PMLOG_TEMPERATURE_EDGE
        private const int PmLogTemperatureHotspot = 27;  // ADL_PMLOG_TEMPERATURE_HOTSPOT
        private const int PmLogTemperatureGfx = 28;      // ADL_PMLOG_TEMPERATURE_GFX

        private static readonly int[] PreferredPmLogTempSensors =
        {
            PmLogTemperatureEdge,
            PmLogTemperatureGfx,
            PmLogTemperatureHotspot
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLPMLogDataOutput
        {
            public int iSize;

            // Flattened ADLSingleSensorData[256]: { supported, value } pairs.
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PmLogDataInts)]
            public int[] Data;
        }
    }
}
