# Router

**Status:** implemented (windows)

The router knows more about the network than a scan can find: every device that has ever joined, how each one is connected, Wi-Fi signal, traffic and the broadband line. Petaurum reads the router's own status pages and shows that data in the app's native style, so the user never has to wait for the router's slow web UI.

## Supported routers

- **AT&T BGW gateways** (Arris/Humax BGW210, BGW320 and similar), recognised by a `/cgi-bin/home.ha` status page at the default gateway's address. Tested on a BGW320-500.

Other routers are added one at a time; an unrecognised router simply has no router pages.

## Reading

- Petaurum **must** only read. It never submits a router form except the access code sign-in, never follows links that change settings, and never presses buttons such as *Restart*, *Clear and Rescan for Devices* or *Reset*.
- The router answers one request at a time, and some pages take 30 seconds or more because the router collects their data before it sends anything. Petaurum **must** send one request at a time and give each one at least 90 seconds.
- A page is read as a list of **sections** (each `h2` heading). A section holds:
  - **fields**: label and value rows;
  - **records**: groups of fields separated by rules, such as one group per device on the device list;
  - **tables**: grids with column headings, such as Wi-Fi status per band or Wi-Fi clients.
- Text is kept as the router shows it, including line breaks and the alt text of status images (for example `Wi-Fi 2 bars`). Buttons, forms and the *Help* section are left out.

### Pages

| Page | Router path | Access code |
|------|-------------|-------------|
| Status | `home.ha` | no |
| Device List | `devices.ha` | no |
| Home Network | `lanstatistics.ha` | no |
| Broadband | `broadbandstatistics.ha` | no |
| Fiber | `fiberstat.ha` | no |
| System Information | `sysinfo.ha` | no |
| Firewall | `firewall.ha` | no |
| Voice | `voice.ha` | no |
| Logs | `logs.ha` | no |
| IP Allocation | `ipalloc.ha` | yes |
| NAT Table | `nattable.ha` | yes |
| Event Log | `events.ha` | yes |

A page that has content before its first heading gets an untitled first section. Form fields are never read, so settings such as Wi-Fi passwords are never copied. Configuration pages (Wi-Fi, firewall rules, DHCP) are mostly form fields and are left for later.

Some routers fail some pages (the BGW320-500 tested answers *System Information* and *Broadband* with `500 Internal Server Error`); that page then shows its last good copy, or says it couldn't be read.

## Remembered data

- Every page Petaurum reads is stored on this computer with the time it was read.
- Opening a page **must** show its stored copy straight away, with when it was read, and then refresh it in the background. The stored copy stays on screen until the fresh one arrives.
- At launch, Petaurum refreshes every page in the background, one at a time, starting with the page on screen.
- The user **must** be able to refresh a page on demand.
- If a refresh fails, the stored copy stays, and the page says the refresh failed and why.

## Device list

What the router knows about each device also shows in Petaurum's own [device list](device-list.md), matched by MAC address. It comes from the stored *Device List* and *Home Network* pages, so it appears at launch and updates whenever those pages refresh.

- **Connection:** from the device's *Connection Type*: `Ethernet port N` for `Ethernet LAN-N`, or `Wi-Fi` and the band (`Wi-Fi 2.4 GHz`, `Wi-Fi 5 GHz`).
- **Signal:** the *Signal Strength* of the device's row in Home Network's *Wi-Fi Client Connection Statistics* table, in dBm.
- **Name:** the device's *Name*, or the name part of *IPv4 Address / Name*, as a name source (see [Device](../domain/device.md#names)).
- **Devices no scan found:** a device on the router's list that isn't in Petaurum's list is added, with the router's IP if it has one. It's online if the router says its *Status* is `on`, and otherwise offline, last seen at its *Last Activity* (the router's local time).

## Access code

Some pages need the router's *device access code*, printed on the router's label.

- When a page needs the code and none is stored, the page says so and offers to sign in. The user types the code into Petaurum; it is never read from environment variables, files or command-line arguments.
- The code **must** be stored only in the operating system's secure credential store, and **must** never be written to logs, files or the remembered page data.
- Sign-in follows the router's own form: fetch the page, take its `nonce`, and post to `/cgi-bin/login.ha` with `hashpassword` = lowercase hex MD5 of the code followed by the nonce, and `password` = one `*` per character of the code. The router keeps the session in a cookie.
- If the router rejects the code, Petaurum forgets it and asks again.
- The user **may** forget the stored code.

## Later

- Configuration pages, read-only first (Wi-Fi, DHCP, firewall rules).
- More routers.

## Platform notes

- **Windows:** router pages are under **Router** in the navigation pane. Stored pages are JSON in `%LOCALAPPDATA%\Petaurum\router\`. The access code is kept in the Windows Credential Locker (`PasswordVault`, resource `Petaurum router`).
