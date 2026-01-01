using System;
using System.IO;
using System.Text.Json;

namespace Vitals.Widget
{
    public sealed class WidgetSettings
    {
        public int X { get; set; } = 100;
        public int Y { get; set; } = 100;

        public bool ShowCpu { get; set; } = true;
        public bool ShowGpu { get; set; } = true;

        // 0.15 = very transparent background, 1.0 = solid background
        public double BackgroundOpacity { get; set; } = 0.66;

        public bool IsLocked { get; set; } = false;

        // Provider order lists (skeleton now, real providers later)
        public string[] GpuProviderOrderWindows { get; set; } = new[] { "nvidia-nvml", "amd-adlx", "intel" };
        public string[] GpuProviderOrderLinux { get; set; } = new[]
{
    "linux-amd-hwmon",
    "linux-nvidia-smi",
    "linux-nvidia-hwmon"
};


        public string[] CpuProviderOrderWindows { get; set; } = new[] { "windows-cpu-wmi" };

        public string[] CpuProviderOrderLinux { get; set; } = new[] { "linux-cpu-hwmon" };

    }

    public static class WidgetSettingsStore
    {
        private static string GetSettingsPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VitalsWidget");

            Directory.CreateDirectory(dir);

            return Path.Combine(dir, "settings.json");
        }

        public static WidgetSettings Load()
        {
            var path = GetSettingsPath();

            if (!File.Exists(path))
                return new WidgetSettings();

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<WidgetSettings>(json);

                return settings ?? new WidgetSettings();
            }
            catch
            {
                // If the file is corrupt, just fall back to defaults.
                return new WidgetSettings();
            }
        }

        public static void Save(WidgetSettings settings)
        {
            var path = GetSettingsPath();

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }
    }
}
