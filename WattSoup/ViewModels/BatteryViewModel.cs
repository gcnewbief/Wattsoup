using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WattSoup.Models;

namespace WattSoup.ViewModels;

/// <summary>
/// Bindable view of a single battery. One instance per physical battery so that
/// machines with multiple batteries (e.g. ThinkPads) each get their own tab/card.
/// </summary>
public sealed partial class BatteryViewModel : ViewModelBase
{
    // Level thresholds for the percentage colour.
    private const int LowPercent = 15;
    private const int MediumPercent = 40;
    // Health considered "good" at or above this value.
    private const double HealthGoodPercent = 70.0;

    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush AmberBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush NeutralBrush = new SolidColorBrush(Color.Parse("#8A93A2"));

    /// <summary>Stable key used to match this VM to a battery across refreshes.</summary>
    public string Key { get; }

    [ObservableProperty] private string _name = "Battery";
    [ObservableProperty] private int _percentage;
    [ObservableProperty] private string _status = "Loading…";
    [ObservableProperty] private string _timeRemaining = "N/A";
    [ObservableProperty] private string _health = "N/A";
    [ObservableProperty] private string _cycleCount = "N/A";
    [ObservableProperty] private string _voltage = "N/A";
    [ObservableProperty] private string _power = "N/A";
    [ObservableProperty] private string _modelName = "Unknown";
    [ObservableProperty] private string _serialNumber = "N/A";
    [ObservableProperty] private string _manufactureDate = "N/A";
    [ObservableProperty] private string _fullChargeCapacity = "N/A";
    [ObservableProperty] private string _designCapacity = "N/A";

    // Colour brushes recomputed whenever the underlying number changes.
    [ObservableProperty] private IBrush _percentageBrush = GreenBrush;
    [ObservableProperty] private IBrush _healthBrush = NeutralBrush;
    [ObservableProperty] private IBrush _powerBrush = NeutralBrush;

    public BatteryViewModel(BatteryInfo info)
    {
        Key = info.Name;
        Update(info);
    }

    public void Update(BatteryInfo info)
    {
        Name = info.Name;
        Percentage = info.Percentage ?? 0;
        Status = info.Status;
        ModelName = info.ModelName ?? info.Name;
        SerialNumber = info.SerialNumber ?? "N/A";
        ManufactureDate = info.ManufactureDate?.ToString("yyyy-MM-dd") ?? "N/A";
        TimeRemaining = FormatTime(info.TimeRemaining);
        Health = info.HealthPercent.HasValue ? $"{info.HealthPercent.Value:0.#}%" : "N/A";
        CycleCount = info.CycleCount?.ToString() ?? "N/A";
        Voltage = info.VoltageVolts.HasValue ? $"{info.VoltageVolts.Value:0.00} V" : "N/A";
        Power = FormatPower(info.PowerWatts, info.Status);
        FullChargeCapacity = FormatCapacity(info.FullChargeCapacity, info.CapacityUnit);
        DesignCapacity = FormatCapacity(info.DesignCapacity, info.CapacityUnit);

        PercentageBrush = BrushForPercentage(info.Percentage ?? 0);
        HealthBrush = BrushForHealth(info.HealthPercent);
        PowerBrush = BrushForPower(info.Status);
    }

    private static bool IsCharging(string status)
        => status.Equals("Charging", StringComparison.OrdinalIgnoreCase);

    private static bool IsDischarging(string status)
        => status.Equals("Discharging", StringComparison.OrdinalIgnoreCase);

    private static IBrush BrushForPower(string status)
        => IsCharging(status) ? GreenBrush
         : IsDischarging(status) ? RedBrush
         : NeutralBrush;

    /// <summary>
    /// Power reads as a magnitude from sysfs; prefix "-" while discharging to signal
    /// energy leaving the battery. Charging/idle stay unsigned.
    /// </summary>
    private static string FormatPower(double? watts, string status)
    {
        if (watts is null)
            return "N/A";
        string sign = IsDischarging(status) ? "-" : "";
        return $"{sign}{watts.Value:0.0} W";
    }

    private static IBrush BrushForPercentage(int percent)
        => percent < LowPercent ? RedBrush
         : percent < MediumPercent ? AmberBrush
         : GreenBrush;

    private static IBrush BrushForHealth(double? health)
    {
        if (health is null)
            return NeutralBrush;
        return health.Value >= HealthGoodPercent ? GreenBrush : RedBrush;
    }

    private static string FormatCapacity(double? value, string unit)
    {
        if (value is null)
            return "N/A";
        // mAh reads better as an integer; Wh keeps one decimal.
        return unit == "mAh" ? $"{value.Value:0} {unit}" : $"{value.Value:0.0} {unit}";
    }

    private static string FormatTime(TimeSpan? span)
    {
        if (span is null)
            return "N/A";
        var t = span.Value;
        return t.Hours > 0 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m";
    }
}
