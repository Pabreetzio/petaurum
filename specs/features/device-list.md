# Device list

**Status:** implemented (windows)

The first screen: every device Petaurum can see on the local network.

## Behaviour

- The app **must** scan once on launch, without any setup.
- The user **must** be able to start a scan on demand ("Scan now").
- Only one scan runs at a time; asking again while one is running does nothing.
- While scanning, the status line says what it is doing (pinging, reading the neighbour table, resolving names).
- After a scan, the status line shows the device count and the time of the scan.
- If a scan fails, the status line says so, and the previous results stay visible.

## Discovery

A scan finds devices using only what the operating system can see without extra software or privileges:

1. **Pick subnets.** Use each active IPv4 interface that has a default gateway. If none has one, use every active non-loopback IPv4 interface. Virtual adapters without a gateway (VMs, WSL) are skipped this way.
2. **Limit size.** If a subnet has more than 1024 host addresses, scan only the /24 around the local address.
3. **Ping sweep.** Send one ICMP echo to every host address, in parallel, with a short timeout. Even devices that ignore ping usually answer the ARP request this triggers, which fills the neighbour table.
4. **Read the neighbour (ARP) table.** Keep entries inside the scanned subnets that have a valid unicast MAC.
5. **Merge.** A device is anything in the ARP table, anything that answered ping, and the local computer itself.
6. **Resolve names.** Look up each IP with reverse DNS, with a timeout per lookup. Always use the OS machine name for the local computer, because DNS often maps it to something unhelpful (Docker adds `host.docker.internal`, for example).

## Display

One row per device, columns in this order: **Name**, **IP address**, **MAC address**. Follow the display rules in [Device](../domain/device.md). Clicking an IP opens `http://<ip>/` in the default browser.

## Later

- Remember devices between scans and show when each was last seen.
- More name sources: mDNS/Bonjour, NetBIOS, SSDP/UPnP.
- Vendor from the MAC's OUI prefix.
- Detect which ports serve a web UI (80, 443, 8080…) and link to the right one, including https.
- Scan on a schedule.

## Platform notes

- **Windows:** the app lives in the system tray. Closing the window hides it; Exit is in the tray menu.
