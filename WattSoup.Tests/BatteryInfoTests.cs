using WattSoup.Models;
using Xunit;

namespace WattSoup.Tests;

public class BatteryInfoTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        var info = new BatteryInfo();

        Assert.Equal("Unknown", info.Name);
        Assert.Equal("Unknown", info.Status);
        Assert.Equal("mAh", info.CapacityUnit);
        Assert.False(info.IsCharging);
        Assert.False(info.IsDischarging);
        Assert.Null(info.Percentage);
    }

    [Theory]
    [InlineData("Charging", true)]
    [InlineData("charging", true)]
    [InlineData("Discharging", false)]
    [InlineData("Full", false)]
    public void IsCharging_MatchesStatusCaseInsensitively(string status, bool expected)
    {
        Assert.Equal(expected, new BatteryInfo { Status = status }.IsCharging);
    }

    [Theory]
    [InlineData("Discharging", true)]
    [InlineData("DISCHARGING", true)]
    [InlineData("Charging", false)]
    [InlineData("Full", false)]
    public void IsDischarging_MatchesStatusCaseInsensitively(string status, bool expected)
    {
        Assert.Equal(expected, new BatteryInfo { Status = status }.IsDischarging);
    }
}
