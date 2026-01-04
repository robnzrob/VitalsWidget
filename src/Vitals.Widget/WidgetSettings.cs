using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Vitals.Widget
{
    public sealed class WidgetSettings
    {
        // Display options
        public bool UseFahrenheit { get; set; } = false;   // default Celsius
        public bool ShowLabels { get; set; } = true;       // show "CPU"/"GPU" text
        public bool ShowUnits { get; set; } = true;        // show the C/F suffix (default on)


        public int FontSize { get; set; } = 22;

        // Keep width fixed (v1) but configurable. Height stays SizeToContent.
        public int WidgetWidth { get; set; } = 130;

        public int X { get; set; } = 100;
        public int Y { get; set; } = 100;

        public bool ShowCpu { get; set; } = true;
        public bool ShowGpu { get; set; } = true;

        // 0.15 = very transparent background, 1.0 = solid background
        public double BackgroundOpacity { get; set; } = 0.66;

        public bool IsLocked { get; set; } = false;

        // Provider order lists.
        // Important: these arrays also act as the "allow list" of provider keys this build knows about.
        // On load we will:
        // - keep user order for known keys
        // - remove unknown keys (including providers removed from the app)
        // - append new defaults the user doesn't yet have
        public string[] GpuProviderOrderWindows { get; set; } = new[] { "nvidia-nvml", "amd-adlx", "intel" };
        public string[] GpuProviderOrderLinux { get; set; } = new[] { "linux-nvidia-smi", "linux-amd-hwmon", "linux-nvidia-hwmon" };

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

            // Always start from the defaults of this build.
            // This is the reference for "known" provider keys and default order.
            var defaults = new WidgetSettings();

            if (!File.Exists(path))
                return defaults;

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<WidgetSettings>(json);

                if (loaded == null)
                    return defaults;

                var merged = MergeWithDefaults(loaded, defaults, out var changed);

                // If we had to clean up / upgrade provider lists, persist it
                // so older settings files self-heal after an update.
                if (changed)
                    Save(merged);

                return merged;
            }
            catch
            {
                // If the file is corrupt, just fall back to defaults.
                return defaults;
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

        private static WidgetSettings MergeWithDefaults(WidgetSettings loaded, WidgetSettings defaults, out bool changed)
        {
            changed = false;

            // Scalars: keep the user's values. Defaults exist mainly so missing JSON fields get a sane value.
            var merged = new WidgetSettings
            {
                UseFahrenheit = loaded.UseFahrenheit,
                ShowLabels = loaded.ShowLabels,
                ShowUnits = loaded.ShowUnits,


                X = loaded.X,
                Y = loaded.Y,

                ShowCpu = loaded.ShowCpu,
                ShowGpu = loaded.ShowGpu,

                BackgroundOpacity = loaded.BackgroundOpacity,
                IsLocked = loaded.IsLocked,
                FontSize = loaded.FontSize,
                WidgetWidth = loaded.WidgetWidth,

            };

            // Provider lists: treat defaults as the allow list of keys this build supports.
            merged.GpuProviderOrderWindows = UpgradeOrder(
                loaded.GpuProviderOrderWindows,
                defaults.GpuProviderOrderWindows,
                out var c1);

            merged.GpuProviderOrderLinux = UpgradeOrder(
                loaded.GpuProviderOrderLinux,
                defaults.GpuProviderOrderLinux,
                out var c2);

            merged.CpuProviderOrderWindows = UpgradeOrder(
                loaded.CpuProviderOrderWindows,
                defaults.CpuProviderOrderWindows,
                out var c3);

            merged.CpuProviderOrderLinux = UpgradeOrder(
                loaded.CpuProviderOrderLinux,
                defaults.CpuProviderOrderLinux,
                out var c4);

            changed = c1 || c2 || c3 || c4;
            return merged;
        }

        private static string[] UpgradeOrder(string[]? loadedOrder, string[] defaultOrder, out bool changed)
        {
            changed = false;

            loadedOrder ??= Array.Empty<string>();
            defaultOrder ??= Array.Empty<string>();

            // Defaults define the allowed keys for this build.
            var allowed = new HashSet<string>(defaultOrder, StringComparer.OrdinalIgnoreCase);

            // Keep user order first (for allowed keys only), then append any new defaults they don't have.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var raw in loadedOrder)
            {
                var key = (raw ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                if (!allowed.Contains(key))
                {
                    // Unknown or removed provider key, drop it.
                    changed = true;
                    continue;
                }

                if (!seen.Add(key))
                {
                    // Duplicate entry, drop it.
                    changed = true;
                    continue;
                }

                result.Add(key);
            }

            foreach (var raw in defaultOrder)
            {
                var key = (raw ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                if (seen.Add(key))
                {
                    // New provider key introduced in this build, append it.
                    result.Add(key);
                    changed = true;
                }
            }

            // If the file had an empty/invalid list, fall back to defaults.
            if (result.Count == 0 && defaultOrder.Length > 0)
            {
                changed = true;
                return defaultOrder;
            }

            // If the user list length changed due to cleanup (unknown keys/dupes), that's an upgrade.
            if (result.Count != loadedOrder.Length)
                changed = true;

            return result.ToArray();
        }
    }
}
