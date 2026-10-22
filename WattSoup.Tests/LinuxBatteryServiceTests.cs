using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WattSoup.Models;
using WattSoup.Services;
using Xunit;

namespace WattSoup.Tests;

public class LinuxBatteryServiceTests
{
    // ---- ParseUevent --------------------------------------------------------

    [Fact]
    public void ParseUevent_ParsesKeyValuePairs_AndTrims()
    {
        const string content = "POWER_SUPPLY_NAME=BAT0\nPOWER_SUPPLY_STATUS= Charging \n  POWER_SUPPLY_CAPACITY =77 ";

        var values = LinuxBatteryService.ParseUevent(content);

        Assert.Equal("BAT0", values["POWER_SUPPLY_NAME"]);
        Assert.Equal("Charging", values["POWER_SUPPLY_STATUS"]);
        Assert.Equal("77", values["POWER_SUPPLY_CAPACITY"]);
    }

    [Fact]
    public void ParseUevent_SkipsBlankAndMalformedLines()
    {
        const string content = "GOOD=1\n\nno_equals_here\n=leadingEquals\nALSO=2\n";

        var values = LinuxBatteryService.ParseUevent(content);

        Assert.Equal(2, values.Count);
        Assert.Equal("1", values["GOOD"]);
        Assert.Equal("2", values["ALSO"]);
        Assert.False(values.ContainsKey(""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseUevent_NullOrEmpty_ReturnsEmpty(string? content)
    {
        Assert.Empty(LinuxBatteryService.ParseUevent(content));
    }

    // ---- ScaleMicro / ParseInt / ParseLong / NullIfBlank --------------------

    [Fact]
    public void ScaleMicro_DividesByOneMillion()
    {
        Assert.Equal(12.0, LinuxBatteryService.ScaleMicro(12_000_000));
        Assert.Null(LinuxBatteryService.ScaleMicro(null));
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-3", -3)]
    [InlineData("", null)]
    [InlineData("nope", null)]
    [InlineData(null, null)]
    public void ParseInt_HandlesValidAndInvalid(string? input, int? expected)
    {
        Assert.Equal(expected, LinuxBatteryService.ParseInt(input));
    }

    [Theory]
    [InlineData("5000000", 5000000L)]
    [InlineData("bad", null)]
    [InlineData(null, null)]
    public void ParseLong_HandlesValidAndInvalid(string? input, long? expected)
    {
        Assert.Equal(expected, LinuxBatteryService.ParseLong(input));
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("  abc  ", "abc")]
    public void NullIfBlank_TrimsAndNullsBlanks(string? input, string? expected)
    {
        Assert.Equal(expected, LinuxBatteryService.NullIfBlank(input));
    }

    // ---- CalcHealth ---------------------------------------------------------

    [Fact]
    public void CalcHealth_RoundsToOneDecimal()
    {
        Assert.Equal(83.3, LinuxBatteryService.CalcHealth(5_000_000, 6_000_000));
    }

    [Theory]
    [InlineData(0L, 6000L)]
    [InlineData(5000L, 0L)]
    [InlineData(null, 6000L)]
    [InlineData(5000L, null)]
    [InlineData(-1L, 6000L)]
    public void CalcHealth_ReturnsNull_WhenInputsNonPositiveOrMissing(long? now, long? design)
    {
        Assert.Null(LinuxBatteryService.CalcHealth(now, design));
    }

    // ---- CalcTime -----------------------------------------------------------

    [Fact]
    public void CalcTime_Discharging_UsesNowOverRate()
    {
        var t = LinuxBatteryService.CalcTime("Discharging", now: 2_500_000, full: null, rate: 1_000_000);
        Assert.Equal(TimeSpan.FromHours(2.5), t);
    }

    [Fact]
    public void CalcTime_Charging_UsesRemainingOverRate()
    {
        var t = LinuxBatteryService.CalcTime("Charging", now: 25_000_000, full: 50_000_000, rate: 10_000_000);
        Assert.Equal(TimeSpan.FromHours(2.5), t);
    }

    [Fact]
    public void CalcTime_Charging_NullWhenFullMissing()
    {
        Assert.Null(LinuxBatteryService.CalcTime("Charging", now: 10, full: null, rate: 5));
    }

    [Theory]
    [InlineData("Full")]
    [InlineData("Not charging")]
    [InlineData("Unknown")]
    public void CalcTime_NonChargeDischargeStatus_ReturnsNull(string status)
    {
        Assert.Null(LinuxBatteryService.CalcTime(status, now: 10, full: 20, rate: 5));
    }

    [Fact]
    public void CalcTime_ZeroRate_ReturnsNull()
    {
        Assert.Null(LinuxBatteryService.CalcTime("Discharging", now: 10, full: 20, rate: 0));
    }

    [Fact]
    public void CalcTime_NullNow_ReturnsNull()
    {
        Assert.Null(LinuxBatteryService.CalcTime("Discharging", now: null, full: 20, rate: 5));
    }

    [Fact]
    public void CalcTime_IsCaseInsensitiveForStatus()
    {
        var t = LinuxBatteryService.CalcTime("discharging", now: 3_000_000, full: null, rate: 1_000_000);
        Assert.Equal(TimeSpan.FromHours(3), t);
    }

    // ---- BuildBatteryInfo: Amp-based sample (Dell GD1JP65 per AGENTS.md) -----

    private static Dictionary<string, string> AmpSample() => new(StringComparer.Ordinal)
    {
        ["POWER_SUPPLY_NAME"] = "BAT0",
        ["POWER_SUPPLY_STATUS"] = "Discharging",
        ["POWER_SUPPLY_CAPACITY"] = "42",
        ["POWER_SUPPLY_CYCLE_COUNT"] = "0",
        ["POWER_SUPPLY_VOLTAGE_NOW"] = "12000000",
        ["POWER_SUPPLY_CHARGE_NOW"] = "2500000",
        ["POWER_SUPPLY_CHARGE_FULL"] = "5000000",
        ["POWER_SUPPLY_CHARGE_FULL_DESIGN"] = "6000000",
        ["POWER_SUPPLY_CURRENT_NOW"] = "1000000",
        ["POWER_SUPPLY_TECHNOLOGY"] = "Li-poly",
        ["POWER_SUPPLY_MODEL_NAME"] = "DELL GD1JP65",
        ["POWER_SUPPLY_MANUFACTURER"] = "SMP",
        ["POWER_SUPPLY_SERIAL_NUMBER"] = "  ",
    };

    [Fact]
    public void BuildBatteryInfo_AmpBased_ComputesMetrics()
    {
        var info = LinuxBatteryService.BuildBatteryInfo(AmpSample(), "fallback", manufactureDate: null);

        Assert.False(info.IsWattBased);
        Assert.Equal("mAh", info.CapacityUnit);
        Assert.Equal("BAT0", info.Name);
        Assert.Equal("Discharging", info.Status);
        Assert.Equal(42, info.Percentage);
        Assert.Equal(12.0, info.VoltageVolts);
        Assert.Equal(83.3, info.HealthPercent);
        Assert.Equal(5000.0, info.FullChargeCapacity);
        Assert.Equal(6000.0, info.DesignCapacity);
        Assert.Equal(12.0, info.PowerWatts); // V (12) * A (1)
        Assert.Equal(TimeSpan.FromHours(2.5), info.TimeRemaining);
        Assert.Null(info.CycleCount); // 0 reported -> null
        Assert.Null(info.SerialNumber); // blank -> null
        Assert.Equal("SMP", info.Manufacturer);
    }

    [Fact]
    public void BuildBatteryInfo_UsesFallbackName_WhenNameAbsent()
    {
        var values = AmpSample();
        values.Remove("POWER_SUPPLY_NAME");

        var info = LinuxBatteryService.BuildBatteryInfo(values, "BATX", manufactureDate: null);

        Assert.Equal("BATX", info.Name);
    }

    // ---- BuildBatteryInfo: Watt-based sample --------------------------------

    private static Dictionary<string, string> WattSample() => new(StringComparer.Ordinal)
    {
        ["POWER_SUPPLY_NAME"] = "BAT0",
        ["POWER_SUPPLY_STATUS"] = "Charging",
        ["POWER_SUPPLY_CAPACITY"] = "50",
        ["POWER_SUPPLY_CYCLE_COUNT"] = "100",
        ["POWER_SUPPLY_VOLTAGE_NOW"] = "11000000",
        ["POWER_SUPPLY_ENERGY_NOW"] = "25000000",
        ["POWER_SUPPLY_ENERGY_FULL"] = "50000000",
        ["POWER_SUPPLY_ENERGY_FULL_DESIGN"] = "60000000",
        ["POWER_SUPPLY_POWER_NOW"] = "10000000",
    };

    [Fact]
    public void BuildBatteryInfo_WattBased_ComputesMetrics()
    {
        var info = LinuxBatteryService.BuildBatteryInfo(WattSample(), "fallback", manufactureDate: null);

        Assert.True(info.IsWattBased);
        Assert.Equal("Wh", info.CapacityUnit);
        Assert.Equal(50, info.Percentage);
        Assert.Equal(83.3, info.HealthPercent);
        Assert.Equal(50.0, info.FullChargeCapacity); // µWh -> Wh
        Assert.Equal(60.0, info.DesignCapacity);
        Assert.Equal(10.0, info.PowerWatts); // power_now scaled, not V*A
        Assert.Equal(TimeSpan.FromHours(2.5), info.TimeRemaining); // (full-now)/rate
        Assert.Equal(100, info.CycleCount);
    }

    [Fact]
    public void BuildBatteryInfo_EmptyValues_ProducesSafeDefaults()
    {
        var info = LinuxBatteryService.BuildBatteryInfo(
            new Dictionary<string, string>(), "BAT0", manufactureDate: null);

        Assert.Equal("BAT0", info.Name);
        Assert.Equal("Unknown", info.Status);
        Assert.Null(info.Percentage);
        Assert.Null(info.HealthPercent);
        Assert.Null(info.TimeRemaining);
        Assert.Null(info.PowerWatts);
        Assert.False(info.IsWattBased);
        Assert.Equal("mAh", info.CapacityUnit);
    }

    [Fact]
    public void BuildBatteryInfo_PassesThroughManufactureDate()
    {
        var date = new DateOnly(2020, 6, 15);
        var info = LinuxBatteryService.BuildBatteryInfo(AmpSample(), "BAT0", date);
        Assert.Equal(date, info.ManufactureDate);
    }

    // ---- ReadAllBatteries: fixture directory tree ---------------------------

    private static string CreateBatteryDir(string root, string name, string type, string uevent)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "type"), type);
        File.WriteAllText(Path.Combine(dir, "uevent"), uevent);
        return dir;
    }

    [Fact]
    public void ReadAllBatteries_OnlyReturnsBatteryTypeDirs_OrderedByName()
    {
        var root = Path.Combine(Path.GetTempPath(), "wattsoup_" + Guid.NewGuid().ToString("N"));
        try
        {
            // Intentionally created out of order to prove the Ordinal sort.
            CreateBatteryDir(root, "BAT1", "Battery", "POWER_SUPPLY_NAME=BAT1\nPOWER_SUPPLY_STATUS=Full\n");
            CreateBatteryDir(root, "BAT0", "Battery", "POWER_SUPPLY_NAME=BAT0\nPOWER_SUPPLY_STATUS=Full\n");
            CreateBatteryDir(root, "AC", "Mains", "POWER_SUPPLY_NAME=AC\n");

            var batteries = LinuxBatteryService.ReadAllBatteries(root);

            Assert.Equal(2, batteries.Count);
            Assert.Equal(new[] { "BAT0", "BAT1" }, batteries.Select(b => b.Name).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadAllBatteries_ReadsManufactureDateFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "wattsoup_" + Guid.NewGuid().ToString("N"));
        try
        {
            var dir = CreateBatteryDir(root, "BAT0", "Battery", "POWER_SUPPLY_NAME=BAT0\nPOWER_SUPPLY_STATUS=Full\n");
            File.WriteAllText(Path.Combine(dir, "manufacture_year"), "2021");
            File.WriteAllText(Path.Combine(dir, "manufacture_month"), "3");
            File.WriteAllText(Path.Combine(dir, "manufacture_day"), "9");

            var battery = Assert.Single(LinuxBatteryService.ReadAllBatteries(root));
            Assert.Equal(new DateOnly(2021, 3, 9), battery.ManufactureDate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadAllBatteries_NonexistentRoot_ReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "wattsoup_missing_" + Guid.NewGuid().ToString("N"));
        Assert.Empty(LinuxBatteryService.ReadAllBatteries(missing));
    }

    [Fact]
    public async Task GetBatteriesAsync_ReturnsFixtureBatteries()
    {
        var root = Path.Combine(Path.GetTempPath(), "wattsoup_" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateBatteryDir(root, "BAT0", "Battery", "POWER_SUPPLY_NAME=BAT0\nPOWER_SUPPLY_STATUS=Full\n");
            var service = new LinuxBatteryService(root);

            var batteries = await service.GetBatteriesAsync();

            var battery = Assert.Single(batteries);
            Assert.Equal("BAT0", battery.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
