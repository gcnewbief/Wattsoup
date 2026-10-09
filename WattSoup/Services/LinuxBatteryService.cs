using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WattSoup.Models;

namespace WattSoup.Services;

/// <summary>
/// Reads battery data from the Linux sysfs virtual filesystem at
/// /sys/class/power_supply/. Handles both Watt-based (energy_*/power_now) and
/// Amp-based (charge_*/current_now) hardware, and never hardcodes "BAT0".
///
/// Discovered on the target machine (Dell GD1JP65): Amp-based, keys present are
/// CHARGE_NOW, CHARGE_FULL, CHARGE_FULL_DESIGN, CURRENT_NOW, VOLTAGE_NOW,
/// CYCLE_COUNT, CAPACITY, STATUS, TECHNOLOGY, MODEL_NAME, MANUFACTURER.
/// </summary>
public sealed class LinuxBatteryService : IBatteryService
{
    private const string DefaultPowerSupplyRoot = "/sys/class/power_supply";

    private readonly string _powerSupplyRoot;

    public LinuxBatteryService()
        : this(DefaultPowerSupplyRoot)
    {
    }

    /// <summary>
    /// Test/DI hook: point the reader at an arbitrary root so fixture directories can
    /// stand in for the real sysfs tree.
    /// </summary>
    internal LinuxBatteryService(string powerSupplyRoot)
    {
        _powerSupplyRoot = powerSupplyRoot;
    }

    public Task<IReadOnlyList<BatteryInfo>> GetBatteriesAsync(CancellationToken cancellationToken = default)
    {
        // File I/O against sysfs is cheap and synchronous; wrap in Task for the interface.
        return Task.Run(() => ReadAllBatteries(_powerSupplyRoot), cancellationToken);
    }

    internal static IReadOnlyList<BatteryInfo> ReadAllBatteries(string powerSupplyRoot)
    {
        var batteries = new List<BatteryInfo>();
        foreach (var dir in FindBatteryDirectories(powerSupplyRoot))
        {
            var info = ReadBattery(dir);
            if (info is not null)
                batteries.Add(info);
        }

        // Deterministic ordering by name (BAT0, BAT1, ...) so tabs don't reshuffle.
        return batteries.OrderBy(b => b.Name, StringComparer.Ordinal).ToList();
    }

    private static BatteryInfo? ReadBattery(string batteryDir)
    {
        // uevent contains all key=value pairs in one file; fall back to individual files if absent.
        var values = ParseUevent(ReadFileSafe(Path.Combine(batteryDir, "uevent")));
        return BuildBatteryInfo(values, Path.GetFileName(batteryDir), ReadManufactureDate(batteryDir));
    }

    /// <summary>
    /// Pure mapping from parsed uevent key/value pairs (plus the two file-sourced inputs
    /// that cannot live in uevent) to an immutable <see cref="BatteryInfo"/>. Performs the
    /// unit-basis selection (Watt vs Amp) and all derived math. No file I/O, so it is
    /// directly unit-testable with in-memory fixtures.
    /// </summary>
    internal static BatteryInfo BuildBatteryInfo(
        IReadOnlyDictionary<string, string> values,
        string fallbackName,
        DateOnly? manufactureDate)
    {
        string status = values.GetValueOrDefault("POWER_SUPPLY_STATUS") ?? "Unknown";
        int? capacity = ParseInt(values.GetValueOrDefault("POWER_SUPPLY_CAPACITY"));
        int? cycleCount = ParseInt(values.GetValueOrDefault("POWER_SUPPLY_CYCLE_COUNT"));

        double? voltageV = ScaleMicro(ParseLong(values.GetValueOrDefault("POWER_SUPPLY_VOLTAGE_NOW")));

        // --- Determine measurement basis: prefer Watt (energy/power), fall back to Amp (charge/current). ---
        long? energyNow = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_ENERGY_NOW"));
        long? energyFull = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_ENERGY_FULL"));
        long? energyFullDesign = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_ENERGY_FULL_DESIGN"));
        long? powerNow = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_POWER_NOW"));

        long? chargeNow = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_CHARGE_NOW"));
        long? chargeFull = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_CHARGE_FULL"));
        long? chargeFullDesign = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_CHARGE_FULL_DESIGN"));
        long? currentNow = ParseLong(values.GetValueOrDefault("POWER_SUPPLY_CURRENT_NOW"));

        bool wattBased = energyFull.HasValue && powerNow.HasValue;

        double? healthPercent;
        TimeSpan? timeRemaining;
        double? powerWatts;
        double? fullChargeCapacity;
        double? designCapacity;
        string capacityUnit;

        if (wattBased)
        {
            healthPercent = CalcHealth(energyFull, energyFullDesign);
            powerWatts = ScaleMicro(powerNow);
            timeRemaining = CalcTime(status, energyNow, energyFull, powerNow);
            // µWh -> Wh
            fullChargeCapacity = ScaleMicro(energyFull);
            designCapacity = ScaleMicro(energyFullDesign);
            capacityUnit = "Wh";
        }
        else
        {
            healthPercent = CalcHealth(chargeFull, chargeFullDesign);
            // Power (W) = Voltage (V) * Current (A) when we only have Amp data.
            powerWatts = (voltageV.HasValue && currentNow.HasValue)
                ? voltageV.Value * ScaleMicro(currentNow)!.Value
                : null;
            timeRemaining = CalcTime(status, chargeNow, chargeFull, currentNow);
            // µAh -> mAh
            fullChargeCapacity = chargeFull.HasValue ? chargeFull.Value / 1_000.0 : null;
            designCapacity = chargeFullDesign.HasValue ? chargeFullDesign.Value / 1_000.0 : null;
            capacityUnit = "mAh";
        }

        return new BatteryInfo
        {
            Name = values.GetValueOrDefault("POWER_SUPPLY_NAME") ?? fallbackName,
            Status = status,
            Percentage = capacity,
            VoltageVolts = voltageV,
            CycleCount = cycleCount is > 0 ? cycleCount : null,
            Technology = values.GetValueOrDefault("POWER_SUPPLY_TECHNOLOGY"),
            ModelName = values.GetValueOrDefault("POWER_SUPPLY_MODEL_NAME"),
            Manufacturer = values.GetValueOrDefault("POWER_SUPPLY_MANUFACTURER"),
            SerialNumber = NullIfBlank(values.GetValueOrDefault("POWER_SUPPLY_SERIAL_NUMBER")),
            ManufactureDate = manufactureDate,
            FullChargeCapacity = fullChargeCapacity,
            DesignCapacity = designCapacity,
            CapacityUnit = capacityUnit,
            HealthPercent = healthPercent,
            TimeRemaining = timeRemaining,
            PowerWatts = powerWatts,
            IsWattBased = wattBased,
            CapturedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Some batteries (e.g. certain ThinkPads) expose the manufacture date via separate
    /// manufacture_year/month/day files. These are NOT in uevent and are usually absent.
    /// Returns null unless at least a valid year is present.
    /// </summary>
    private static DateOnly? ReadManufactureDate(string batteryDir)
    {
        int? year = ParseInt(ReadFileSafe(Path.Combine(batteryDir, "manufacture_year"))?.Trim());
        if (year is not > 0)
            return null;

        int month = ParseInt(ReadFileSafe(Path.Combine(batteryDir, "manufacture_month"))?.Trim()) ?? 1;
        int day = ParseInt(ReadFileSafe(Path.Combine(batteryDir, "manufacture_day"))?.Trim()) ?? 1;

        month = Math.Clamp(month, 1, 12);
        day = Math.Clamp(day, 1, DateTime.DaysInMonth(year.Value, month));

        try
        {
            return new DateOnly(year.Value, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Enumerates the power_supply directory and returns every entry whose
    /// "type" file reads "Battery". Never assumes a specific name like BAT0, and
    /// supports machines with multiple batteries (BAT0, BAT1, ...).
    /// </summary>
    private static IEnumerable<string> FindBatteryDirectories(string powerSupplyRoot)
    {
        if (!Directory.Exists(powerSupplyRoot))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(powerSupplyRoot))
        {
            var type = ReadFileSafe(Path.Combine(dir, "type"));
            if (string.Equals(type?.Trim(), "Battery", StringComparison.OrdinalIgnoreCase))
                yield return dir;
        }
    }

    /// <summary>
    /// Parses raw uevent text (one KEY=value per line) into a dictionary. Blank lines and
    /// lines without a '=' are skipped; keys and values are trimmed. A null/empty input
    /// yields an empty dictionary.
    /// </summary>
    internal static Dictionary<string, string> ParseUevent(string? content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(content))
            return result;

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;
            result[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        return result;
    }

    // --- Calculation helpers -------------------------------------------------

    internal static double? CalcHealth(long? fullNow, long? fullDesign)
    {
        if (fullNow is > 0 && fullDesign is > 0)
            return Math.Round((double)fullNow.Value / fullDesign.Value * 100.0, 1);
        return null;
    }

    /// <summary>
    /// Remaining time. When discharging: now / rate. When charging: (full - now) / rate.
    /// "rate" and "now"/"full" must share the same unit basis (both Amp or both Watt).
    /// Returns null when the rate is zero (e.g. "Not charging") to avoid divide-by-zero.
    /// </summary>
    internal static TimeSpan? CalcTime(string status, long? now, long? full, long? rate)
    {
        if (rate is not > 0 || now is null)
            return null;

        double hours;
        if (status.Equals("Charging", StringComparison.OrdinalIgnoreCase))
        {
            if (full is not > 0)
                return null;
            hours = (double)(full.Value - now.Value) / rate.Value;
        }
        else if (status.Equals("Discharging", StringComparison.OrdinalIgnoreCase))
        {
            hours = (double)now.Value / rate.Value;
        }
        else
        {
            // Full / Not charging: no meaningful estimate.
            return null;
        }

        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
            return null;

        return TimeSpan.FromHours(hours);
    }

    // --- Parsing helpers -----------------------------------------------------

    /// <summary>sysfs reports micro-units (µV, µA, µWh). Convert to base unit.</summary>
    internal static double? ScaleMicro(long? micro)
        => micro.HasValue ? micro.Value / 1_000_000.0 : null;

    internal static int? ParseInt(string? s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    internal static long? ParseLong(string? s)
        => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    internal static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Reads a file, returning null on any IO/permission error instead of throwing.</summary>
    private static string? ReadFileSafe(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
