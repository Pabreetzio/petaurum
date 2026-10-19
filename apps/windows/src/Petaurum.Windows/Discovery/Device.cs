using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;

namespace Petaurum.Windows.Discovery;

/// <summary>A device one scan found on the local network. Devices/DeviceItem is the remembered device.</summary>
public sealed record Device(
    IPAddress IpAddress,
    PhysicalAddress? MacAddress,
    string? HostName,
    string? Vendor,
    bool IsThisComputer,
    bool RespondedToPing,
    DateTimeOffset LastSeen)
{
    /// <summary>Numeric sort key so .9 sorts before .10.</summary>
    public uint SortKey => BinaryPrimitives.ReadUInt32BigEndian(IpAddress.GetAddressBytes());
}
