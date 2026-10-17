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
6. **Resolve names.** Ask every name source at once, and pick each device's name as described in [Device](../domain/device.md#names). Always use the OS machine name for the local computer, because DNS often maps it to something unhelpful (Docker adds `host.docker.internal`, for example).
   - **Reverse DNS:** look up each IP, with a timeout per lookup.
   - **NetBIOS:** send a node status query (UDP 137, name `*`) to each IP and collect replies until a deadline. Windows and Samba machines and most NAS boxes answer.
   - **mDNS / DNS-SD:** send one-shot queries (RFC 6762 §5.1) from an ordinary UDP port, so answers come back by unicast and nothing has to bind port 5353. Ask for:
     - the reverse PTR (`<d.c.b.a>.in-addr.arpa`) of each IP;
     - a browse (PTR query) of `_services._dns-sd._udp.local` and of common service types: web (`_http`), printers (`_ipp`, `_ipps`, `_printer`, `_pdl-datastream`, `_scanner`, `_uscan`), Apple (`_airplay`, `_raop`, `_companion-link`, `_device-info`, `_afpovertcp`), Google Cast (`_googlecast`), speakers (`_spotify-connect`, `_sonos`), file and remote access (`_smb`, `_sftp-ssh`, `_ssh`, `_workstation`), and smart home (`_hap`, `_homekit`, `_hue`). Service types found by the `_services` browse are browsed too, once the device IPs are known.

     Send the browse to the multicast group `224.0.0.251:5353` as soon as the scan starts, so replies arrive during the ping sweep. Once the device IPs are known, send each device its reverse PTR query and the browse straight to its port 5353, and send the reverse PTR queries to the group as well. Both matter: some devices (Google Cast) answer only queries sent straight to them, and others only the group.

     Use every record in a reply (answers and additional records): A records map host names to IPs, SRV records map service instances to host names, and TXT records carry extra names. An instance whose host name can't be resolved belongs to the IP that sent the reply.
   - Send each query twice, a short interval apart, because UDP can drop packets.
   - All sources share one deadline of about 2 seconds, so a scan of a /24 still takes only a few seconds.
7. **Find vendors.** Look up each MAC address as described in [Device](../domain/device.md#vendor). This needs no network traffic.

## Display

One row per device, columns in this order: **Name**, **Vendor**, **IP address**, **MAC address**. Follow the display rules in [Device](../domain/device.md). Clicking an IP opens `http://<ip>/` in the default browser.

## Later

- Remember devices between scans and show when each was last seen.
- More name sources: SSDP/UPnP (friendly names and models from device descriptions), LLMNR.
- Detect which ports serve a web UI (80, 443, 8080…) and link to the right one, including https.
- Scan on a schedule.

## Platform notes

- **Windows:** the app lives in the system tray. Closing the window hides it; Exit is in the tray menu.
