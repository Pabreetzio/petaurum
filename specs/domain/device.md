# Device

**Status:** draft

A device is anything on the local network that Petaurum has observed.

## Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `ipAddress` | IPv4 string | no | Missing only for a device known just from the router, which lists some offline devices without one. |
| `macAddress` | string, `AA:BB:CC:DD:EE:FF` | no | Upper-case hex, colon separated. |
| `hostName` | string | no | Best name we found (see *Names*). |
| `customName` | string | no | A name the user gave the device. Overrides every other name. |
| `vendor` | string | no | Maker of the network interface, from the MAC's OUI prefix (see *Vendor*). |
| `isThisComputer` | bool | yes | True for the machine running the app. |
| `respondedToPing` | bool | yes | Whether it answered ICMP echo during the last scan. |
| `firstSeen` | timestamp | yes | When a scan first observed it. |
| `lastSeen` | timestamp | yes | When a scan, or the router, last observed it. |
| `connection` | string | no | How it reaches the router, from the router (see [Router](../features/router.md#device-list)), e.g. `Ethernet port 3` or `Wi-Fi 5 GHz`. |
| `signal` | integer, dBm | no | Wi-Fi signal strength at the router, e.g. `-62`. |

## Identity

A device is identified by its MAC address when known, because that stays the same when DHCP hands out a new IP. A device with no known MAC is identified by its IP address. When a device first seen by IP alone later shows up with a MAC at that IP, the two are the same device.

Phones and computers that use private (locally administered) MACs may change them over time; each new MAC is a new device.

## Names

A device can have several names, one per source. The best one becomes `hostName`, preferring names a person chose over names a machine made up:

1. **mDNS service name**: the instance name a device advertises over DNS-SD, e.g. `Kitchen speaker` or `Office printer`. Some services carry a friendlier name inside it or in a TXT record: AirPlay audio (`_raop`) prefixes the name with the MAC and `@`, which is dropped, and Google Cast puts the name the user chose in the TXT key `fn`. A service name that looks machine-generated (a UUID or a long run of hex digits) is skipped.
2. **UPnP friendly name**: the `friendlyName` in the root device description a device points to from its SSDP reply, e.g. `Shed Speakers`. Some devices append their IP in parentheses (`C4-VDB-W(192.168.1.239)`); that part is dropped.
3. **Reverse DNS**: the PTR record for the IP, usually the DHCP host name the router recorded.
4. **Router name**: the name on the router's device list, usually the DHCP host name too. It also names devices that are offline.
5. **mDNS host name**: the `.local` name the device answers to, from a reverse PTR query or an A record.
6. **NetBIOS name**: the unique workstation name (suffix `<00>`) from a NetBIOS node status query.
7. **UPnP model**: when a device description has no usable friendly name, its `manufacturer` and `modelName` (e.g. `LUMA`), or failing those the domain of its `deviceType` when that names a model rather than a schema like `schemas-upnp-org` (`urn:AN-210-SW-8-POE:device:Switch:1` gives `AN-210-SW-8-POE`). This says what a device is, not which one it is, so it comes last.

Names are trimmed and lose any trailing dot. A name that is just the IP address is ignored, and so is a name from mDNS, UPnP or the router that looks machine-generated: a UUID, a long run of hex digits (such as `unknown0c02bd322d7e`), or one with no letters.

## Vendor

The vendor comes from the IEEE registry of MAC prefixes, using the longest match: MA-S (36 bits), then MA-M (28 bits), then MA-L (24 bits, the OUI). Legal suffixes such as `Inc.`, `Co., Ltd.` and `GmbH` are dropped to keep it short. Entries the registry marks `Private` have no vendor.

A MAC with the *locally administered* bit set (second-lowest bit of the first octet) was not assigned by a vendor. Phones and computers use these as private, randomised addresses. Such a device has no vendor, and is shown as having a private address.

## Display rules

- **Name:** the custom name, else the host name, else `Unknown device`. Add `(this computer)` for the local machine.
- **IP:** links to `http://<ip>/`.
- **MAC:** shown in a monospace font.
- **Connection:** the connection, with the signal strength for Wi-Fi (`Wi-Fi 5 GHz · -62 dBm`).
- **Vendor:** the vendor, or `Private address` for a locally administered MAC.
- When the MAC or vendor is unknown, leave it out, or show `—` where the layout needs a placeholder (a table column, say).
- Lists sort by IP address numerically (so `.9` comes before `.10`), not alphabetically.
