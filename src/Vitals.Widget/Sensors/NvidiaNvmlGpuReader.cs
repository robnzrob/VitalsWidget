using System;
using System.Runtime.InteropServices;

namespace Vitals.Widget.Sensors
{
    public static class NvidiaNvmlGpuReader
    {
        // nvmlTemperatureSensors_t: NVML_TEMPERATURE_GPU
        private const uint NVML_TEMPERATURE_GPU = 0;

        private static bool _initAttempted;
        private static bool _initOk;
        private static IntPtr _device;

        public static bool TryGetGpuTempC(out int tempC)
        {
            tempC = 0;

            if (!EnsureInit())
                return false;

            uint temp;
            var rc = nvmlDeviceGetTemperature(_device, NVML_TEMPERATURE_GPU, out temp);
            if (rc != 0)
                return false;

            tempC = unchecked((int)temp);

            // sanity guard
            return tempC > 0 && tempC < 150;
        }

        private static bool EnsureInit()
        {
            if (_initOk)
                return true;

            if (_initAttempted)
                return false;

            _initAttempted = true;

            try
            {
                var rc = nvmlInit_v2();
                if (rc != 0)
                    return false;

                rc = nvmlDeviceGetHandleByIndex_v2(0, out _device);
                if (rc != 0 || _device == IntPtr.Zero)
                {
                    nvmlShutdown();
                    return false;
                }

                _initOk = true;
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                // very old nvml.dll
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Shutdown()
        {
            if (!_initOk)
                return;

            try
            {
                nvmlShutdown();
            }
            catch
            {
            }
            finally
            {
                _initOk = false;
                _initAttempted = false;
                _device = IntPtr.Zero;
            }
        }

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint temp);
    }
}
