# Petaurum for Windows

A native Windows tray app (.NET 9, WPF).

## Run

```bash
dotnet run --project apps/windows/src/Petaurum.Windows
```

Pass `--minimized` to start in the tray without opening the window:

```bash
dotnet run --project apps/windows/src/Petaurum.Windows -- --minimized
```

## What it does

- Lives in the notification area. Left-click the icon to open the window; right-click for **Open**, **Scan now** and **Exit**.
- Closing the window hides it to the tray. Only one copy runs at a time.
- Implements [Device list](../../specs/features/device-list.md): a ping sweep of each local subnet, then the Windows ARP table (`GetIpNetTable`), then reverse DNS for names.

## Layout

```
src/Petaurum.Windows/
  App.xaml(.cs)          Startup, single instance, tray lifetime
  MainWindow.xaml(.cs)   Device list window
  DeviceListViewModel.cs Scan state and results
  Discovery/             Subnets, ping sweep, ARP table, name lookup
  Tray/                  Tray icon and placeholder app icon
```

WinForms is referenced only for `NotifyIcon`, since WPF has no tray icon of its own.
