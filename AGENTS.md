# AGENTS.md — WattSoup

> Read this file first. It contains the hard rules and known traps for this codebase.
> Companion docs: `PLAN.md` (architecture) and `TASKS.md` (what to build next).

## What this project is
**WattSoup** is a cross-platform C# desktop app that displays Linux battery health
(percentage, time remaining, wear/health, cycle count, voltage, power) in a modern
dark dashboard.

## Tech stack (DO NOT change without explicit user approval)
- **.NET 8** (`net8.0`)
- **Avalonia 11** UI (Fluent theme) — NOT WinForms, NOT WPF. WinForms throws
  `PlatformNotSupportedException` on Linux.
- **CommunityToolkit.Mvvm** for MVVM (source-generated `[ObservableProperty]`).
  DO NOT introduce ReactiveUI.

## Golden rules
1. **UI thread marshaling.** The battery is polled on a background timer. Any write to
   an `[ObservableProperty]` MUST happen inside
   `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(...)`. Writing from the timer
   thread crashes Avalonia. See `MainViewModel.RefreshAsync`.
2. **Never hardcode `BAT0`.** Enumerate `/sys/class/power_supply/` and pick the folder
   whose `type` file reads `Battery`. See `LinuxBatteryService.FindBatteryDirectory`.
3. **Safe file reads only.** Never use bare `File.ReadAllText`. Use `ReadFileSafe`
   (checks `File.Exists`, catches exceptions, returns null). Missing metrics -> `null`
   -> UI shows "N/A". Battery controllers drop files across kernel updates.
4. **Never mix Amp and Watt units.** Prefer Watt basis (`energy_now`/`power_now`); fall
   back to Amp basis (`charge_now`/`current_now`). Do not divide a charge by a power.
   See `LinuxBatteryService.ReadPrimaryBattery`.
5. **sysfs reports micro-units** (µV, µA, µWh). Divide by 1_000_000. Use `ScaleMicro`.
6. **Avalonia XAML != WPF XAML.** Use `BoxShadow` not `DropShadowEffect`, use Avalonia
   controls. Don't paste WPF XAML with renamed namespaces.

## Target hardware (this machine, discovered via terminal)
- Battery folder: `BAT0`, model `DELL GD1JP65`, manufacturer `SMP`.
- **Amp-based** (charge/current), NOT Watt-based. Keys present:
  `CHARGE_NOW, CHARGE_FULL, CHARGE_FULL_DESIGN, CURRENT_NOW, VOLTAGE_NOW,
  CYCLE_COUNT (reports 0), CAPACITY, STATUS, TECHNOLOGY, MODEL_NAME, MANUFACTURER`.
- `energy_*` / `power_now` files are **absent** here — the Watt code path is untested on
  this machine; test it on Watt-based hardware or with fixtures.

## Build & run
```bash
# .NET SDK is NOT installed yet on this machine. Install one first:
sudo snap install dotnet-sdk --classic   # provides .NET 8

dotnet restore
dotnet build
dotnet run --project WattSoup
```

## Definition of done for any task
- `dotnet build` succeeds with no new warnings.
- No `[ObservableProperty]` is written off the UI thread.
- No `File.ReadAllText` / hardcoded `BAT0` was introduced.
- Update `TASKS.md`: tick the completed task, add any follow-ups.
