# TASKS.md — WattSoup Backlog

> For continuation agents. Read `AGENTS.md` before starting. Tick a box when done and
> add follow-ups. Keep each change minimal and buildable.

## Done (skeleton)
- [x] Project scaffold: `.csproj`, `Program.cs`, `App.axaml(.cs)`, `ViewLocator.cs`.
- [x] `Models/BatteryInfo.cs` record.
- [x] `Services/IBatteryService.cs` + `Services/LinuxBatteryService.cs` (dynamic BAT
      discovery, Amp/Watt handling, safe reads, micro-unit scaling).
- [x] `ViewModels/MainViewModel.cs` (3s polling + Dispatcher marshaling).
- [x] `Views/MainWindow.axaml` dark dashboard (basic).

## Done (features round 2)
- [x] **Multiple batteries.** Service enumerates ALL `Battery`-type folders;
      `MainViewModel` holds `ObservableCollection<BatteryViewModel>`; `MainWindow`
      shows a `TabControl` (one tab per battery, rendered via `ViewLocator` ->
      `Views/BatteryView.axaml`). VMs updated in place so the selected tab is kept.
- [x] **New fields**: serial number, manufacture date (from `manufacture_year/month/day`
      when present, else N/A), full-charge capacity + design capacity (Wh for Watt-based,
      mAh for Amp-based).
- [x] **Colour coding** (in `BatteryViewModel`):
      percentage green >=40, amber 15-39, red <15; health green >=70%, red <70%
      (neutral grey when unknown). Exposed as `IBrush` properties bound to `Foreground`.

## Verify first (before adding features)
- [x] **BUILD-1** `dotnet restore && dotnet build` — succeeds, 0 warnings. Note: machine
      has .NET 10 SDK; the .NET 8 runtime was installed via `apt install dotnet-runtime-8.0`
      so the `net8.0` app runs natively (no roll-forward needed).
- [x] **BUILD-2** `dotnet run --project WattSoup` launches and runs stably through poll
      cycles. (Fixed: removed unsupported `Grid.RowSpacing/ColumnSpacing` in MainWindow.axaml.)

## UI
- [ ] **UI-1** Replace the flat `ProgressBar` in `Views/BatteryView.axaml` with a circular
      progress ring (Avalonia `Arc` / `PathGeometry`). Bind its stroke to the existing
      `PercentageBrush`. See the `TODO(agent)` marker in the XAML.
- [ ] **UI-2** Add a charging bolt icon when `Status == "Charging"`.
- [ ] **UI-3** Optional: adopt `FluentAvalonia` or `SukiUI` for a more premium look
      (get user approval first; it's a new dependency).

## Data / service
- [x] **SVC-1** Add fixture-based tests: feed sample `uevent` text (both an Amp sample
      and a Watt sample) into a parser and assert health/time math. Refactor the parse
      logic out of `ReadBattery` into a pure, testable method to enable this.
      Done: extracted `BuildBatteryInfo` (pure) + `ParseUevent`, made calc/parse helpers
      `internal`, and added an internal root-injecting ctor so fixture directories can
      stand in for `/sys`. New `WattSoup.Tests` xUnit project (72 tests) covers
      `LinuxBatteryService` (~93% lines), `BatteryViewModel` (100%), `BatteryInfo`.
      Follow-ups: `MainViewModel` (Dispatcher/timer) and the Views/App bootstrap are
      still uncovered — need an Avalonia headless test harness.
- [ ] **SVC-2** Smooth `current_now` with a short moving average — instantaneous values
      are noisy and make "time remaining" jump around.
- [ ] **SVC-3** Optionally read `AC/online` to explicitly detect the adapter.

## Cross-platform (later, needs approval)
- [ ] **XP-1** `WindowsBatteryService` (WMI `Win32_Battery`) selected via OS check in
      `App.axaml.cs`.

## Testing checklist for each PR
- [ ] `dotnet build` clean, no new warnings.
- [ ] No `[ObservableProperty]` written off the UI thread.
- [ ] No hardcoded `BAT0`, no bare `File.ReadAllText`.
