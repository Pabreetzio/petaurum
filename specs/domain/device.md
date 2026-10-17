# Device

**Status:** draft

A device is anything on the local network that Petaurum has observed.

## Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `ipAddress` | IPv4 string | yes | The identity of a device for now (see *Identity*). |
| `macAddress` | string, `AA:BB:CC:DD:EE:FF` | no | Upper-case hex, colon separated. |
| `hostName` | string | no | Best name we found (see *Names*). |
| `vendor` | string | no | Maker of the network interface, from the MAC's OUI prefix (see *Vendor*). |
| `isThisComputer` | bool | yes | True for the machine running the app. |
| `respondedToPing` | bool | yes | Whether it answered ICMP echo during the last scan. |
| `lastSeen` | timestamp | yes | When the last scan observed it. |

## Identity

For now a device is identified by its IP address. A MAC address is more stable across DHCP leases, so once devices are persisted, identity should move to the MAC when known and fall back to the IP.

## Names

A device can have several names, one per source. The best one becomes `hostName`, preferring names a person chose over names a machine made up:

1. **mDNS service name**: the instance name a device advertises over DNS-SD, e.g. `Kitchen speaker` or `Office printer`. Some services carry a friendlier name inside it or in a TXT record: AirPlay audio (`_raop`) prefixes the name with the MAC and `@`, which is dropped, and Google Cast puts the name the user chose in the TXT key `fn`. A service name that looks machine-generated (a UUID or a long run of hex digits) is skipped.
2. **Reverse DNS**: the PTR record for the IP, usually the DHCP host name the router recorded.
3. **mDNS host name**: the `.local` name the device answers to, from a reverse PTR query or an A record.
4. **NetBIOS name**: the unique workstation name (suffix `<00>`) from a NetBIOS node status query.

Names are trimmed and lose any trailing dot. A name that is just the IP address is ignored.

## Vendor

The vendor comes from the IEEE registry of MAC prefixes, using the longest match: MA-S (36 bits), then MA-M (28 bits), then MA-L (24 bits, the OUI). Legal suffixes such as `Inc.`, `Co., Ltd.` and `GmbH` are dropped to keep it short. Entries the registry marks `Private` have no vendor.

A MAC with the *locally administered* bit set (second-lowest bit of the first octet) was not assigned by a vendor. Phones and computers use these as private, randomised addresses. Such a device has no vendor, and is shown as having a private address.

## Display rules

- **Name:** the host name, or `—` when unknown. Add `(this computer)` for the local machine.
- **IP:** shown as a link to `http://<ip>/`.
- **MAC:** shown in a monospace font, or `—` when unknown.
- **Vendor:** the vendor, `Private address` for a locally administered MAC, or `—` when unknown.
- Lists sort by IP address numerically (so `.9` comes before `.10`), not alphabetically.
