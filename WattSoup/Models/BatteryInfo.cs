namespace WattSoup.Models;

/// <summary>
/// Immutable snapshot of a battery's state at a single point in time.
/// All nullable numeric fields are null when the underlying /sys file is missing,
/// so the UI can render "N/A" instead of crashing.
/// </summary>
public sealed record BatteryInfo
{
    /// <summary>Power supply folder name, e.g. "BAT0".</summary>
    public string Name { get; init; } = "Unknown";

    /// <summary>Charging status: "Charging", "Discharging", "Full", "Not charging", "Unknown".</summary>
    public string Status { get; init; } = "Unknown";

    /// <summary>Reported charge percentage 0-100 (from CAPACITY).</summary>
    public int? Percentage { get; init; }

    /// <summary>Current voltage in Volts (VOLTAGE_NOW / 1_000_000).</summary>
    public double? VoltageVolts { get; init; }

    /// <summary>Manufacturer-reported charge cycle count. May be 0 or null if unsupported.</summary>
    public int? CycleCount { get; init; }

    /// <summary>Battery chemistry, e.g. "Li-poly".</summary>
    public string? Technology { get; init; }

    public string? ModelName { get; init; }
    public string? Manufacturer { get; init; }

    /// <summary>Manufacturer battery serial number.</summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// Manufacture date if the hardware exposes it (rare; some ThinkPads do via
    /// manufacture_year/month/day). Null when unavailable.
    /// </summary>
    public System.DateOnly? ManufactureDate { get; init; }

    /// <summary>
    /// Current fully-charged capacity (what the battery holds now when full).
    /// Expressed in <see cref="CapacityUnit"/>. Null if source files missing.
    /// </summary>
    public double? FullChargeCapacity { get; init; }

    /// <summary>Original factory design capacity, in <see cref="CapacityUnit"/>.</summary>
    public double? DesignCapacity { get; init; }

    /// <summary>Unit for the capacity fields: "Wh" for Watt-based, "mAh" for Amp-based.</summary>
    public string CapacityUnit { get; init; } = "mAh";

    /// <summary>
    /// Health / wear level as a percentage of original design capacity remaining (0-100).
    /// Calculated from full_current / full_design. Null if the source files are missing.
    /// </summary>
    public double? HealthPercent { get; init; }

    /// <summary>
    /// Estimated time until full (charging) or empty (discharging).
    /// Null when the device reports zero current or the required files are missing.
    /// </summary>
    public System.TimeSpan? TimeRemaining { get; init; }

    /// <summary>Instantaneous power draw/charge rate in Watts (positive number).</summary>
    public double? PowerWatts { get; init; }

    /// <summary>True when the measurement is based on energy/power (Watt) files rather than charge/current (Amp) files.</summary>
    public bool IsWattBased { get; init; }

    /// <summary>UTC timestamp of when this snapshot was captured.</summary>
    public System.DateTime CapturedAtUtc { get; init; } = System.DateTime.UtcNow;

    public bool IsCharging => Status.Equals("Charging", System.StringComparison.OrdinalIgnoreCase);
    public bool IsDischarging => Status.Equals("Discharging", System.StringComparison.OrdinalIgnoreCase);
}
