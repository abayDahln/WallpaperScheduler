# Wallpaper Scheduler

WinUI 3 desktop app for scheduled wallpaper rotation on Windows 11. Set a
weekly schedule with multiple time slots per day, add recurring or one-time
overrides, and let the app swap your wallpaper automatically in the background.

## Features

- **Wallpaper Library** — import, rename, preview, and delete wallpapers
- **Weekly Schedule** — set multiple time slots per day, each with its own wallpaper
- **Overrides** — monthly recurring overrides (e.g. every 17th) and one-time date overrides
- **Settings** — auto-start on boot, minimize-to-tray, theme (system dark/light), wallpaper style (fit/fill/etc.)
- **System Tray** — pause/resume the schedule, open the app, quick exit

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK 2.x is bundled via self-contained deployment — no separate install needed

## Build & Run

```bash
# Build
dotnet build -p:Platform=x64

# Run
dotnet run --framework net8.0-windows10.0.19041.0 -p:Platform=x64

# Publish (Release)
dotnet publish -c Release -p:Platform=x64
```

Platforms: `x64`, `x86`, `ARM64`. Always pass `-p:Platform=...` to `dotnet`
commands — the project targets multiple platforms.

> Tip: launch with `--tray` to start minimized to the system tray
> (also how the auto-start shortcut behaves).

## Project Structure

```
├── Views/          # XAML pages (Library, Weekly Schedule, Overrides, Settings)
├── ViewModels/     # MVVM view models (CommunityToolkit.Mvvm)
├── Services/       # Config, Scheduler, Wallpaper apply, AutoStart, Theme
├── Models/         # Data models (AppConfig, WallpaperItem, TimeSlot, Override)
├── Helpers/        # ScheduleResolver
├── Assets/         # Icons and splash screen
├── Properties/     # launchSettings.json
└── documentation/  # PRD, requirements, architecture, design docs
```

## Data Locations

Config and wallpapers live under `%LocalAppData%\WallpaperSchedule\`:

```
%LocalAppData%\WallpaperSchedule\config.json    # app configuration + schedule
%LocalAppData%\WallpaperSchedule\Wallpapers\    # imported wallpaper files
%LocalAppData%\WallpaperSchedule\crash.log      # unhandled exception log
```

## Tech Stack

- WinUI 3 (Windows App SDK 2.x), unpackaged / self-contained
- .NET 8 (`net8.0-windows10.0.19041.0`)
- CommunityToolkit.Mvvm 8.x
- H.NotifyIcon (system tray)

## License

MIT