# Petaurum

> A springboard to every connected device on your network.

Your network is full of little web servers — the TV, cameras, speakers, router, NAS, gaming console, computers — each with its own local UI hidden behind an IP address nobody remembers. Petaurum finds them and gets you to each one in a click.

It runs **on your own network** (edge first). It starts as a launchpad, and over time it will learn each device's API, store credentials securely, and let you talk to your devices directly.

*Petaurum* is Latin for the springboard Roman acrobats launched from. Its cousin *Petaurus* is the sugar glider genus — small animals that leap and glide from one thing to the next.

## Repo layout

This is a monorepo. Each platform gets its own **native** app, and they all follow the same shared specs.

```
docs/            Pitch, roadmap, and decisions
specs/           Platform-neutral specs: domain models, features, behaviour
apps/
  windows/       Windows tray app (.NET / WinUI 3)
```

Specs come first: when a behaviour changes, update `specs/` and then each app that implements it.

## Apps

| App | Status | Stack |
|-----|--------|-------|
| [Windows](apps/windows/README.md) | Device list from ARP + ping sweep, names from DNS/NetBIOS/mDNS, vendors, tray icon | .NET 10, WinUI 3 |

## Roadmap

1. **Find** — list devices on the local network: host name, IP, MAC.
2. **Launch** — link straight to each device's local web UI.
3. **Identify** — recognise what each device is (vendor, model, type).
4. **Talk** — learn each device's API and control it, with credentials stored securely.
