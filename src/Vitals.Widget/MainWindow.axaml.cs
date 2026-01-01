using Avalonia;
using Avalonia.Media;
using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vitals.Widget.Sensors;

namespace Vitals.Widget;

public partial class MainWindow : Window
{
    private readonly WidgetSettings _settings;
    private Border? _rootBorder;
    private TextBlock? _gpuText;
    private DispatcherTimer? _timer;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public MainWindow()
    {
        InitializeComponent();

        _rootBorder = this.FindControl<Border>("RootBorder");
        _gpuText = this.FindControl<TextBlock>("GpuText");


        _settings = WidgetSettingsStore.Load();

        // Apply saved window position
        Position = new PixelPoint(_settings.X, _settings.Y);

        // Apply saved background opacity
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);

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
            NvidiaNvmlGpuReader.Shutdown();

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

    private void ContextMenu_OnOpened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ContextMenu cm)
            return;

        var lockItem = cm.FindControl<MenuItem>("LockMenuItem");
        if (lockItem == null)
            return;

        lockItem.IsChecked = _settings.IsLocked;
        lockItem.Header = _settings.IsLocked ? "Unlock position" : "Lock position";
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

    private void UpdateReadings()
    {
        if (_gpuText == null)
            return;

        int temp;
        if (NvidiaNvmlGpuReader.TryGetGpuTempC(out temp))
            _gpuText.Text = $"GPU {temp}°C";
        else
            _gpuText.Text = "GPU N/A";
    }

}