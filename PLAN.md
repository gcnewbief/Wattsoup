# PLAN.md — WattSoup Architecture

## Goal
A lightweight, cross-platform (Linux-first) battery health dashboard, filling the gap
between raw CLI tools (`upower -d`, `acpi -V`) and DE-locked widgets — a
CoconutBattery-style UI for Linux.

## Layered architecture (MVVM)

```
Program.cs                 -> Avalonia entry point / AppBuilder
App.axaml(.cs)             -> Composition root: builds LinuxBatteryService + MainViewModel
ViewLocator.cs             -> Maps *ViewModel -> *View by naming convention

Models/
  BatteryInfo.cs           -> Immutable record snapshot. Nullable fields => "N/A" in UI.

Services/
  IBatteryService.cs       -> Abstraction (enables mock/fixture services + tests)
  LinuxBatteryService.cs   -> Reads /sys/class/power_supply, parses uevent, does math

ViewModels/
  ViewModelBase.cs         -> ObservableObject base
  MainViewModel.cs         -> 3s polling Timer -> service -> Dispatcher.UIThread -> props

Views/
  MainWindow.axaml(.cs)    -> Dark dashboard: percentage, status, metrics grid
```

## Data flow
1. `MainViewModel` ctor starts an immediate refresh + a `System.Threading.Timer` (3s).
2. Timer callback -> `IBatteryService.GetBatteryInfoAsync()` (runs file I/O off-thread).
3. `LinuxBatteryService` finds the battery dir, reads `uevent`, parses key=value pairs.
4. It picks Watt basis (`energy/power`) if present, else Amp basis (`charge/current`),
   and computes health (`full/design`), time remaining, and power.
5. Result marshaled back via `Dispatcher.UIThread.InvokeAsync` and mapped to bindable
   string properties, formatted for display.

## Key formulas
- **Health %** = `full_now / full_design * 100`
- **Time remaining (discharging)** = `now / rate`
- **Time remaining (charging)** = `(full - now) / rate`
- **Power (Amp basis)** = `voltage_V * current_A`
- Rate == 0 (e.g. "Not charging") => time is `null` (avoid divide-by-zero).

## Cross-platform notes
- The abstraction `IBatteryService` allows future `WindowsBatteryService` (WMI) or
  `MacBatteryService` (IOKit) implementations selected in `App.axaml.cs`.

## Out of scope (for now)
- Charge threshold control (ThinkPad `tp_smapi`, TLP) — read-only app.
- Root/polkit elevation — only read non-privileged sysfs files.
