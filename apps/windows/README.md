# Petaurum for Windows

A native Windows 11 tray app (.NET 10, WinUI 3 / Windows App SDK) in the Fluent Design style of Windows Settings.

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
- The window has a Mica background, a Settings-style navigation pane, and a page of device cards (the Windows Community Toolkit's `SettingsCard`). Click a card to open the device in the browser; right-click to rename, copy its IP or MAC, or forget it. **F2** renames the focused card and **F5** scans again. Light, dark and high-contrast themes and the user's accent colour all come from the system.
- Remembers devices in `%LOCALAPPDATA%\Petaurum\devices.json`, so the list shows instantly at launch and keeps custom names. Devices the latest scan didn't find stay, marked offline.
- On an AT&T BGW gateway, adds **Router** pages to the navigation pane: Status, Device List, Home Network, Wi-Fi clients, logs and more, rebuilt as native cards and tables from the router's status pages. Each page shows its stored copy straight away and refreshes in the background; the router is slow, so requests go one at a time. Pages that need the device access code ask for it once and keep it in Windows Credential Locker.
- Shows what the router knows on the device cards: names for devices a scan couldn't name, the connection (Ethernet port or Wi-Fi band) and Wi-Fi signal strength, and devices that are off, with when the router last saw them.
- Closing the window hides it to the tray. Only one copy runs at a time.
- Implements [Device list](../../specs/features/device-list.md) and [Router](../../specs/features/router.md). Discovery is a ping sweep of each local subnet, then the Windows ARP table (`GetIpNetTable`), then names from reverse DNS, NetBIOS node status and mDNS / DNS-SD all at once, and a vendor from the bundled IEEE MAC prefix list.

## Vendor list

`src/Petaurum.Windows/Discovery/oui.txt` is generated from the IEEE MA-L, MA-M and MA-S registries and embedded in the app. Regenerate it now and then:

```bash
pwsh apps/windows/tools/Update-OuiList.ps1
```

## Layout

```
src/Petaurum.Windows/
  App.xaml(.cs)          Startup, single instance, tray lifetime
  MainWindow.xaml(.cs)   Mica window with a custom title bar and the navigation pane
  Pages/DevicesPage      Device list page, with rename and forget
  Pages/RouterPageView   One router page: field cards, record expanders and tables
  DeviceListViewModel.cs Scan state, merging scans into remembered devices
  Devices/               Remembered devices (DeviceItem) and devices.json
  Router/                BGW client and page parser, stored pages, per-device info, access code in Credential Locker
  Storage/               JSON files under %LOCALAPPDATA%\Petaurum
  Assets/AppIcon.ico     Placeholder app and tray icon
  Discovery/             Subnets, ping sweep, ARP table, name lookups (DNS, NetBIOS, mDNS), vendors
  Tray/                  Tray icon and its menu
tools/
  Update-OuiList.ps1     Regenerates Discovery/oui.txt from the IEEE registry
```

## Design

The app follows Windows 11's Fluent Design rather than a cross-platform look; see [Native look](../../docs/decisions/native-look.md). In practice:

- Use WinUI controls and the Community Toolkit before building custom ones.
- Use theme resources (`{ThemeResource TextFillColorSecondaryBrush}`, `TitleTextBlockStyle` and so on), never hard-coded colours or font sizes.
- Use Segoe Fluent Icons glyphs (`FontIcon`) for icons.
- New pages go in `Pages/` and get an item in `MainWindow`'s `NavigationView`.
- Page content sits in a left-aligned column up to 1000 wide, as in Settings.

The app is unpackaged and carries its own copy of the Windows App SDK runtime (`WindowsAppSDKSelfContained`), so nothing needs installing first. Building needs the .NET 10 SDK. The Windows App SDK's AI, ML, Search and Widgets components are excluded in the project file to keep the output small (about 100 MB instead of 160 MB); bump their versions together with `Microsoft.WindowsAppSDK`. The tray icon comes from [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), since WinUI has none of its own, and router pages are parsed with [AngleSharp](https://anglesharp.github.io/), since their HTML is often malformed.
