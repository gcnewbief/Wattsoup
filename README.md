# WattSoup

A lightweight, cross-platform (Linux-first) **battery health dashboard** written in C#
with **Avalonia 11** and **CommunityToolkit.Mvvm**. It reads directly from the Linux
sysfs interface (`/sys/class/power_supply/`) and presents your laptop's battery state
in a clean, modern dark UI.

![WattSoup icon](WattSoup/Assets/icon.png)

## What it does

WattSoup monitors every battery exposed by your Linux kernel and displays, in real time:

- **Battery percentage** with a progress bar
- **Status** (charging / discharging / full / not charging)
- **Estimated time remaining** until full or empty
- **Health / wear** as a percentage of original design capacity
- **Full-charge capacity** and **design capacity** (mAh or Wh, depending on your hardware)
- **Cycle count**, **voltage**, and **power** (signed: red while discharging, green while charging)
- **Serial number** and **manufacture date** when the battery exposes them

It supports **single and multiple battery machines** (e.g. ThinkPads with `BAT0` and `BAT1`)
through a tabbed interface, and automatically refreshes every three seconds.

## Download / run

No compilation required. The portable build is a **single self-contained executable**;
just copy it to any x64 Linux machine and run it.

```bash
chmod +x WattSoup
./WattSoup
```

Grab the latest portable binary from the [GitHub releases page](https://github.com/gcnewbief/Wattsoup/releases)
(or build it yourself with the command below).

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

```bash
dotnet restore
dotnet run --project WattSoup
```

## Publish a portable binary

Produces a ~44 MB self-contained executable in `publish/` with the runtime and native
graphics libraries embedded.

```bash
dotnet publish WattSoup -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o publish

./publish/WattSoup
```

For other platforms, swap the RID: `linux-arm64`, `win-x64`, `osx-arm64`, etc.

## Install as a desktop app (Linux)

The included script adds a menu launcher with the application icon.
No root is required.

```bash
./packaging/install.sh     # installs into ~/.local/bin and ~/.local/share/...
./packaging/uninstall.sh   # removes it
```

## Project layout

```
WattSoup/
  Program.cs              Avalonia entry point
  App.axaml(.cs)          App bootstrap + composition root
  ViewLocator.cs          ViewModel -> View resolver
  Models/BatteryInfo.cs   Battery snapshot record
  Services/               IBatteryService + LinuxBatteryService (sysfs reader)
  ViewModels/             MainViewModel + BatteryViewModel
  Views/                  MainWindow + BatteryView dashboard
  Assets/                 Application icon
packaging/                .desktop launcher and install/uninstall scripts
AGENTS.md                 Rules for AI contributors (read first)
PLAN.md                   Architecture
TASKS.md                  Backlog
```

## Tech stack

- .NET 8 (`net8.0`)
- Avalonia 11 (cross-platform XAML UI)
- CommunityToolkit.Mvvm (source-generated MVVM)

## License & credits

Created by **Blake Pearn** and **Devin AI**.  
Version **1.0.0**.

For AI contributors: read `AGENTS.md` before making changes.
