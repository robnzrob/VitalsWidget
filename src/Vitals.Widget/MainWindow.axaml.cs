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

    private const int DefaultFontSize = 22;
    private const int MinFontSize = 8;
    private const int MaxFontSize = 32;

    private const int DefaultWidgetWidth = 130;
    private const int MinWidgetWidth = 50;
    private const int MaxWidgetWidth = 260;

    private const double DefaultBackgroundOpacity = 0.35;
    private const double MinBackgroundOpacity = 0.05;
    private const double MaxBackgroundOpacity = 1.0;

    private const double BackgroundStep = 0.08;


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
        Position = ClampToVisibleArea(new PixelPoint(_settings.X, _settings.Y));


        // Apply saved background opacity
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        ApplyWidgetWidth(_settings.WidgetWidth);
        ApplyFontSize(_settings.FontSize);


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
        if (value < MinBackgroundOpacity) value = MinBackgroundOpacity;
        if (value > MaxBackgroundOpacity) value = MaxBackgroundOpacity;

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
    private void ApplyFontSize(int value)
    {
        // Clamp to sensible range for a tiny widget.
        if (value < MinFontSize) value = MinFontSize;
        if (value > MaxFontSize) value = MaxFontSize;



        _settings.FontSize = value;

        if (_cpuText != null) _cpuText.FontSize = value;
        if (_gpuText != null) _gpuText.FontSize = value;
        if (_placeholderText != null) _placeholderText.FontSize = value;

        InvalidateMeasure();
        InvalidateArrange();

        // Keep anchored after height recalculation.
        var p = Position;
        Dispatcher.UIThread.Post(() => Position = p, DispatcherPriority.Background);
    }

    private void ApplyWidgetWidth(int value)
    {
        // Width is a UX choice; keep it within a sane band.
        if (value < MinWidgetWidth) value = MinWidgetWidth;
        if (value > MaxWidgetWidth) value = MaxWidgetWidth;

        _settings.WidgetWidth = value;
        Width = value;

        InvalidateMeasure();
        InvalidateArrange();

        // Width changes can also trigger work-area clamping in docked overlays.
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
    private void Settings_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings, ApplyAllVisualSettings);
        win.Show(this);
    }

    private void ApplyAllVisualSettings()
    {
        ApplyFontSize(_settings.FontSize);
        ApplyWidgetWidth(_settings.WidgetWidth);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
    }
    private string FormatTempText(string label, int tempC)
    {
        var showLabels = _settings.ShowLabels;

        if (_settings.UseFahrenheit)
        {
            var tempF = (int)Math.Round((tempC * 9.0 / 5.0) + 32.0);
            return showLabels ? $"{label} {tempF}°F" : $"{tempF}°F";
        }

        return showLabels ? $"{label} {tempC}°C" : $"{tempC}°C";
    }

    private string FormatNaText(string label)
    {
        return _settings.ShowLabels ? $"{label} N/A" : "N/A";
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
        //User will most likely want to move widget so unlock it.
        _settings.IsLocked = false;

        SetPositionAndPersist(GetCenteredOnCurrentScreen());

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
                _cpuText.Text = FormatTempText("CPU", cpuTemp);
                _cpuText.Foreground = GetTempBrush(cpuTemp);
            }
            else
            {
                _cpuText.Text = FormatNaText("CPU");
                _cpuText.Foreground = GetNeutralBrush();
            }
        }


        // GPU
        if (_gpuText == null || !_settings.ShowGpu)
            return;

        int temp;
        if (_providers.TryGetGpuTempC(out temp))
        {
            _gpuText.Text = FormatTempText("GPU", temp);
            _gpuText.Foreground = GetTempBrush(temp);
        }
        else
        {
            _gpuText.Text = FormatNaText("GPU");
            _gpuText.Foreground = GetNeutralBrush();
        }
    }
    private PixelPoint ClampToVisibleArea(PixelPoint desired)
    {
        var screen = Screens.ScreenFromPoint(desired) ?? Screens.Primary;
        if (screen == null)
            return desired;

        var wa = screen.WorkingArea;

        var w = (int)Math.Ceiling(Width);
        var h = (int)Math.Ceiling(Bounds.Height);

        // Prevent negative max if bounds are weird during early layout
        var maxX = wa.X + wa.Width - Math.Max(1, w);
        var maxY = wa.Y + wa.Height - Math.Max(1, h);

        var x = Math.Clamp(desired.X, wa.X, maxX);
        var y = Math.Clamp(desired.Y, wa.Y, maxY);

        return new PixelPoint(x, y);
    }

    private PixelPoint GetCenteredOnCurrentScreen()
    {
        var screen = Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        if (screen == null)
            return new PixelPoint(50, 50);

        var wa = screen.WorkingArea;

        var w = (int)Math.Ceiling(Width);
        var h = (int)Math.Ceiling(Bounds.Height);

        var x = wa.X + (wa.Width - w) / 2;
        var y = wa.Y + (wa.Height - h) / 2;

        return new PixelPoint(x, y);
    }

    private void SetPositionAndPersist(PixelPoint desired)
    {
        var clamped = ClampToVisibleArea(desired);

        Position = clamped;

        // This is the important bit: re-assert after the context menu closes/layout settles
        Dispatcher.UIThread.Post(() => Position = clamped, DispatcherPriority.Background);

        _settings.X = clamped.X;
        _settings.Y = clamped.Y;
        WidgetSettingsStore.Save(_settings);
    }

}
