using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using Vitals.Widget.Core.Providers;


namespace Vitals.Widget;

public partial class MainWindow : Window
{
    private readonly WidgetSettings _settings;
    private readonly ProviderManager _providers;

    private Border? _rootBorder;
    private TextBlock? _cpuText;
    private TextBlock? _gpuText;
    private TextBlock? _placeholderText;
    private DispatcherTimer? _timer;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainWindow()
    {
        InitializeComponent();

        _rootBorder = this.FindControl<Border>("RootBorder");
        _cpuText = this.FindControl<TextBlock>("CpuText");
        _gpuText = this.FindControl<TextBlock>("GpuText");
        _placeholderText = this.FindControl<TextBlock>("PlaceholderText");

        _settings = WidgetSettingsStore.Load();

        _providers = new ProviderManager(_settings);



        // Apply saved window position
        Position = new PixelPoint(_settings.X, _settings.Y);

        // Apply saved background opacity
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);

        // Apply CPU/GPU visibility immediately (drives auto height via SizeToContent)
        ApplyLineVisibility();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, __) => UpdateReadings();
        _timer.Start();

        UpdateReadings();

        // Save settings when closing
        Closing += (_, __) =>
        {
            _settings.X = Position.X;
            _settings.Y = Position.Y;
            WidgetSettingsStore.Save(_settings);
            _providers.Dispose();


        };

        this.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void ApplyBackgroundOpacity(double value)
    {
        // Clamp to sensible range
        if (value < 0.15) value = 0.15;
        if (value > 1.0) value = 1.0;

        _settings.BackgroundOpacity = value;

        var a = (byte)(value * 255);
        if (_rootBorder != null)
            _rootBorder.Background = new SolidColorBrush(Color.FromArgb(a, 20, 20, 20));
    }

    // Visibility changes affect desired window height (SizeToContent).
    // Some “docked” overlays (eg SidebarDiagnostics) reserve part of the desktop work area.
    // When our height changes, Windows may clamp/reposition the window to keep it inside the work area,
    // which can make it appear to jump left/right.
    // We re-assert the current Position after layout has updated to keep the widget anchored where the user placed it.
    private void ApplyLineVisibility()
    {
        if (_cpuText != null)
            _cpuText.IsVisible = _settings.ShowCpu;

        if (_gpuText != null)
            _gpuText.IsVisible = _settings.ShowGpu;

        if (_placeholderText != null)
            _placeholderText.IsVisible = !_settings.ShowCpu && !_settings.ShowGpu;

        InvalidateMeasure();
        InvalidateArrange();

        // Re-assert Position after the resize so Windows doesn't nudge us out of docked/overlay work areas.
        var p = Position;
        Dispatcher.UIThread.Post(() => Position = p, DispatcherPriority.Background);
    }

    private void Border_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_settings.IsLocked)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Lock_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings.IsLocked = !_settings.IsLocked;
        WidgetSettingsStore.Save(_settings);

        if (sender is MenuItem mi)
        {
            mi.IsChecked = _settings.IsLocked;
            mi.Header = _settings.IsLocked ? "Unlock position" : "Lock position";
        }
    }

    private void ShowCpu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings.ShowCpu = !_settings.ShowCpu;
        WidgetSettingsStore.Save(_settings);
        ApplyLineVisibility();

        if (sender is MenuItem mi)
            mi.IsChecked = _settings.ShowCpu;
    }

    private void ShowGpu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings.ShowGpu = !_settings.ShowGpu;
        WidgetSettingsStore.Save(_settings);
        ApplyLineVisibility();

        if (sender is MenuItem mi)
            mi.IsChecked = _settings.ShowGpu;
    }

    private void ResetPosition_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Position = new PixelPoint(50, 50);
        _settings.X = Position.X;
        _settings.Y = Position.Y;
        WidgetSettingsStore.Save(_settings);
    }

    private void ContextMenu_OnOpened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ContextMenu cm)
            return;

        var lockItem = cm.FindControl<MenuItem>("LockMenuItem");
        if (lockItem != null)
        {
            lockItem.IsChecked = _settings.IsLocked;
            lockItem.Header = _settings.IsLocked ? "Unlock position" : "Lock position";
        }

        var showCpuItem = cm.FindControl<MenuItem>("ShowCpuMenuItem");
        if (showCpuItem != null)
            showCpuItem.IsChecked = _settings.ShowCpu;

        var showGpuItem = cm.FindControl<MenuItem>("ShowGpuMenuItem");
        if (showGpuItem != null)
            showGpuItem.IsChecked = _settings.ShowGpu;
    }

    private void BgMoreSolid_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyBackgroundOpacity(_settings.BackgroundOpacity + 0.08);
        WidgetSettingsStore.Save(_settings);
    }

    private void BgMoreTransparent_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyBackgroundOpacity(_settings.BackgroundOpacity - 0.08);
        WidgetSettingsStore.Save(_settings);
    }

    private void BgReset_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyBackgroundOpacity(0.66);
        WidgetSettingsStore.Save(_settings);
    }

    private void Exit_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private static IBrush GetNeutralBrush()
    {
        return Brushes.Gainsboro;
    }

    private static IBrush GetTempBrush(int tempC)
    {
        // v1 simple scale: green → amber → red
        // These thresholds are intentionally basic and can be tweaked later.
        if (tempC <= 60)
            return Brushes.LimeGreen;

        if (tempC <= 75)
            return Brushes.Gold; // amber

        return Brushes.OrangeRed;
    }


    private void UpdateReadings()
    {
        // CPU 
        if (_cpuText != null && _settings.ShowCpu)
        {
            int cpuTemp;
            if (_providers.TryGetCpuTempC(out cpuTemp))
            {
                _cpuText.Text = $"CPU {cpuTemp}°C";
                _cpuText.Foreground = GetTempBrush(cpuTemp);
            }
            else
            {
                _cpuText.Text = "CPU N/A";
                _cpuText.Foreground = GetNeutralBrush();
            }
        }


        // GPU
        if (_gpuText == null || !_settings.ShowGpu)
            return;

        int temp;
        if (_providers.TryGetGpuTempC(out temp))
        {
            _gpuText.Text = $"GPU {temp}°C";
            _gpuText.Foreground = GetTempBrush(temp);
        }
        else
        {
            _gpuText.Text = "GPU N/A";
            _gpuText.Foreground = GetNeutralBrush();
        }
    }

}
