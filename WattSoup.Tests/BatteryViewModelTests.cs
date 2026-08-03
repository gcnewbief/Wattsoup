using System;
using Avalonia.Media;
using WattSoup.Models;
using WattSoup.ViewModels;
using Xunit;

namespace WattSoup.Tests;

public class BatteryViewModelTests
{
    private static readonly Color Green = Color.Parse("#4ADE80");
    private static readonly Color Amber = Color.Parse("#FBBF24");
    private static readonly Color Red = Color.Parse("#F87171");
    private static readonly Color Neutral = Color.Parse("#8A93A2");

    private static Color ColorOf(IBrush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    // ---- Text formatting ----------------------------------------------------

    [Fact]
    public void Update_FormatsTimeRemaining_WithHoursAndMinutes()
    {
        var vm = new BatteryViewModel(new BatteryInfo { TimeRemaining = TimeSpan.FromMinutes(150) });
        Assert.Equal("2h 30m", vm.TimeRemaining);
    }

    [Fact]
    public void Update_FormatsTimeRemaining_MinutesOnly()
    {
        var vm = new BatteryViewModel(new BatteryInfo { TimeRemaining = TimeSpan.FromMinutes(45) });
        Assert.Equal("45m", vm.TimeRemaining);
    }

    [Fact]
    public void Update_TimeRemaining_NaWhenNull()
    {
        var vm = new BatteryViewModel(new BatteryInfo { TimeRemaining = null });
        Assert.Equal("N/A", vm.TimeRemaining);
    }

    [Fact]
    public void Update_FormatsHealthVoltageCycleCount()
    {
        var vm = new BatteryViewModel(new BatteryInfo
        {
            HealthPercent = 83.3,
            VoltageVolts = 12.345,
            CycleCount = 137,
        });

        Assert.Equal("83.3%", vm.Health);
        Assert.Equal("12.35 V", vm.Voltage);
        Assert.Equal("137", vm.CycleCount);
    }

    [Fact]
    public void Update_NaForMissingHealthVoltageCycleCount()
    {
        var vm = new BatteryViewModel(new BatteryInfo());
        Assert.Equal("N/A", vm.Health);
        Assert.Equal("N/A", vm.Voltage);
        Assert.Equal("N/A", vm.CycleCount);
    }

    [Theory]
    [InlineData("Discharging", "-12.0 W")]
    [InlineData("Charging", "12.0 W")]
    [InlineData("Full", "12.0 W")]
    public void Update_FormatsPowerWithSign(string status, string expected)
    {
        var vm = new BatteryViewModel(new BatteryInfo { PowerWatts = 12.0, Status = status });
        Assert.Equal(expected, vm.Power);
    }

    [Fact]
    public void Update_Power_NaWhenNull()
    {
        var vm = new BatteryViewModel(new BatteryInfo { PowerWatts = null, Status = "Discharging" });
        Assert.Equal("N/A", vm.Power);
    }

    [Fact]
    public void Update_FormatsCapacity_ByUnit()
    {
        var amp = new BatteryViewModel(new BatteryInfo
        {
            FullChargeCapacity = 5000.4,
            DesignCapacity = 6000.0,
            CapacityUnit = "mAh",
        });
        Assert.Equal("5000 mAh", amp.FullChargeCapacity);
        Assert.Equal("6000 mAh", amp.DesignCapacity);

        var watt = new BatteryViewModel(new BatteryInfo
        {
            FullChargeCapacity = 50.25,
            CapacityUnit = "Wh",
        });
        Assert.Equal("50.3 Wh", watt.FullChargeCapacity);
    }

    [Fact]
    public void Update_Capacity_NaWhenNull()
    {
        var vm = new BatteryViewModel(new BatteryInfo { FullChargeCapacity = null, CapacityUnit = "Wh" });
        Assert.Equal("N/A", vm.FullChargeCapacity);
    }

    [Fact]
    public void Update_MapsIdentityFields_WithFallbacks()
    {
        var withModel = new BatteryViewModel(new BatteryInfo
        {
            Name = "BAT0",
            ModelName = "DELL GD1JP65",
            SerialNumber = "SN123",
            ManufactureDate = new DateOnly(2020, 6, 15),
            Percentage = 55,
            Status = "Full",
        });

        Assert.Equal("BAT0", withModel.Name);
        Assert.Equal("BAT0", withModel.Key);
        Assert.Equal("DELL GD1JP65", withModel.ModelName);
        Assert.Equal("SN123", withModel.SerialNumber);
        Assert.Equal("2020-06-15", withModel.ManufactureDate);
        Assert.Equal(55, withModel.Percentage);
        Assert.Equal("Full", withModel.Status);
    }

    [Fact]
    public void Update_ModelNameFallsBackToName_AndNaForMissingSerialAndDate()
    {
        var vm = new BatteryViewModel(new BatteryInfo { Name = "BAT1", ModelName = null });
        Assert.Equal("BAT1", vm.ModelName);
        Assert.Equal("N/A", vm.SerialNumber);
        Assert.Equal("N/A", vm.ManufactureDate);
    }

    // ---- Colour brushes -----------------------------------------------------

    [Theory]
    [InlineData(5, "#F87171")]   // < 15 -> red
    [InlineData(30, "#FBBF24")]  // 15-39 -> amber
    [InlineData(40, "#4ADE80")]  // >= 40 -> green
    [InlineData(90, "#4ADE80")]
    public void PercentageBrush_ReflectsThresholds(int percent, string expectedHex)
    {
        var vm = new BatteryViewModel(new BatteryInfo { Percentage = percent });
        Assert.Equal(Color.Parse(expectedHex), ColorOf(vm.PercentageBrush));
    }

    [Fact]
    public void HealthBrush_GreenAtOrAboveThreshold_RedBelow_NeutralWhenUnknown()
    {
        Assert.Equal(Green, ColorOf(new BatteryViewModel(new BatteryInfo { HealthPercent = 70.0 }).HealthBrush));
        Assert.Equal(Red, ColorOf(new BatteryViewModel(new BatteryInfo { HealthPercent = 69.9 }).HealthBrush));
        Assert.Equal(Neutral, ColorOf(new BatteryViewModel(new BatteryInfo { HealthPercent = null }).HealthBrush));
    }

    [Theory]
    [InlineData("Charging", "#4ADE80")]
    [InlineData("Discharging", "#F87171")]
    [InlineData("Full", "#8A93A2")]
    [InlineData("Not charging", "#8A93A2")]
    public void PowerBrush_ReflectsStatus(string status, string expectedHex)
    {
        var vm = new BatteryViewModel(new BatteryInfo { Status = status });
        Assert.Equal(Color.Parse(expectedHex), ColorOf(vm.PowerBrush));
    }

    [Fact]
    public void Update_CanBeCalledAgain_ToRefreshInPlace()
    {
        var vm = new BatteryViewModel(new BatteryInfo { Name = "BAT0", Percentage = 10, Status = "Discharging" });
        Assert.Equal(Red, ColorOf(vm.PercentageBrush));

        vm.Update(new BatteryInfo { Name = "BAT0", Percentage = 80, Status = "Charging" });

        Assert.Equal(80, vm.Percentage);
        Assert.Equal("Charging", vm.Status);
        Assert.Equal(Green, ColorOf(vm.PercentageBrush));
        Assert.Equal("BAT0", vm.Key); // key stays stable across updates
    }
}
