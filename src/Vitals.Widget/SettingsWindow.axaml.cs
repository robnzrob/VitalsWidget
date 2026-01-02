using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Vitals.Widget;

public partial class SettingsWindow : Window
{
    private readonly WidgetSettings _settings;
    private readonly Action _applyToMainWindow;

    private bool _isInitializing = true;

    private TextBlock _fontSizeValue = null!;
    private TextBlock _widthValue = null!;
    private TextBlock _opacityValue = null!;

    private Slider _fontSizeSlider = null!;
    private Slider _widthSlider = null!;
    private Slider _opacitySlider = null!;


    private ComboBox _unitsCombo = null!;
    private CheckBox _showLabelsCheckBox = null!;



    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public SettingsWindow(WidgetSettings settings, Action applyToMainWindow)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _applyToMainWindow = applyToMainWindow ?? throw new ArgumentNullException(nameof(applyToMainWindow));

        InitializeComponent();

        // Grab controls (fail fast if XAML names don’t match)
        _fontSizeValue = this.FindControl<TextBlock>("FontSizeValue")
            ?? throw new InvalidOperationException("Missing control: FontSizeValue");

        _widthValue = this.FindControl<TextBlock>("WidthValue")
            ?? throw new InvalidOperationException("Missing control: WidthValue");

        _opacityValue = this.FindControl<TextBlock>("OpacityValue")
            ?? throw new InvalidOperationException("Missing control: OpacityValue");

        _fontSizeSlider = this.FindControl<Slider>("FontSizeSlider")
            ?? throw new InvalidOperationException("Missing control: FontSizeSlider");

        _widthSlider = this.FindControl<Slider>("WidthSlider")
            ?? throw new InvalidOperationException("Missing control: WidthSlider");

        _opacitySlider = this.FindControl<Slider>("OpacitySlider")
            ?? throw new InvalidOperationException("Missing control: OpacitySlider");

        _unitsCombo = this.FindControl<ComboBox>("UnitsCombo")
            ?? throw new InvalidOperationException("Missing control: UnitsCombo");

        _showLabelsCheckBox = this.FindControl<CheckBox>("ShowLabelsCheckBox")
            ?? throw new InvalidOperationException("Missing control: ShowLabelsCheckBox");

        // Hook events after controls exist
        _unitsCombo.SelectionChanged += UnitsCombo_OnSelectionChanged;
        _showLabelsCheckBox.IsCheckedChanged += ShowLabelsCheckBox_OnChanged;

        // Hook events AFTER we have controls, to avoid ValueChanged firing during XAML load
        _fontSizeSlider.ValueChanged += FontSizeSlider_OnValueChanged;
        _widthSlider.ValueChanged += WidthSlider_OnValueChanged;
        _opacitySlider.ValueChanged += OpacitySlider_OnValueChanged;

        _fontSizeSlider.Value = _settings.FontSize;
        _widthSlider.Value = _settings.WidgetWidth;
        _opacitySlider.Value = _settings.BackgroundOpacity;

        _unitsCombo.SelectedIndex = _settings.UseFahrenheit ? 1 : 0;
        _showLabelsCheckBox.IsChecked = _settings.ShowLabels;


        UpdateLabels();

        _isInitializing = false;
    }

    private void UpdateLabels()
    {
        _fontSizeValue.Text = _settings.FontSize.ToString(CultureInfo.InvariantCulture);
        _widthValue.Text = _settings.WidgetWidth.ToString(CultureInfo.InvariantCulture);
        _opacityValue.Text = _settings.BackgroundOpacity.ToString("0.00", CultureInfo.InvariantCulture);
    }
    private void UnitsCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.UseFahrenheit = _unitsCombo.SelectedIndex == 1;
        WidgetSettingsStore.Save(_settings);

        _applyToMainWindow();
    }

    private void ShowLabelsCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.ShowLabels = _showLabelsCheckBox.IsChecked == true;
        WidgetSettingsStore.Save(_settings);

        _applyToMainWindow();
    }

    private void FontSizeSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.FontSize = (int)Math.Round(e.NewValue);
        WidgetSettingsStore.Save(_settings);

        UpdateLabels();
        _applyToMainWindow();
    }

    private void WidthSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.WidgetWidth = (int)Math.Round(e.NewValue);
        WidgetSettingsStore.Save(_settings);

        UpdateLabels();
        _applyToMainWindow();
    }

    private void OpacitySlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.BackgroundOpacity = e.NewValue;
        WidgetSettingsStore.Save(_settings);

        UpdateLabels();
        _applyToMainWindow();
    }

    private void Reset_OnClick(object? sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        _settings.UseFahrenheit = false;
        _settings.ShowLabels = true;

        _settings.FontSize = 22;
        _settings.WidgetWidth = 130;
        _settings.BackgroundOpacity = 0.66;

        _fontSizeSlider.Value = _settings.FontSize;
        _widthSlider.Value = _settings.WidgetWidth;
        _opacitySlider.Value = _settings.BackgroundOpacity;

        WidgetSettingsStore.Save(_settings);

        UpdateLabels();

        _isInitializing = false;

        _applyToMainWindow();
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
