# Device

**Status:** draft

A device is anything on the local network that Petaurum has observed.

## Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `ipAddress` | IPv4 string | yes | The identity of a device for now (see *Identity*). |
| `macAddress` | string, `AA:BB:CC:DD:EE:FF` | no | Upper-case hex, colon separated. |
| `hostName` | string | no | Best name we found, e.g. from reverse DNS. |
| `isThisComputer` | bool | yes | True for the machine running the app. |
| `respondedToPing` | bool | yes | Whether it answered ICMP echo during the last scan. |
| `lastSeen` | timestamp | yes | When the last scan observed it. |

## Identity

For now a device is identified by its IP address. A MAC address is more stable across DHCP leases, so once devices are persisted, identity should move to the MAC when known and fall back to the IP.

## Display rules

- **Name:** the host name, or `—` when unknown. Add `(this computer)` for the local machine.
- **IP:** shown as a link to `http://<ip>/`.
- **MAC:** shown in a monospace font, or `—` when unknown.
- Lists sort by IP address numerically (so `.9` comes before `.10`), not alphabetically.
