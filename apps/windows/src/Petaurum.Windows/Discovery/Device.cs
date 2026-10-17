using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;

namespace Petaurum.Windows.Discovery;

/// <summary>A device seen on the local network. See specs/domain/device.md.</summary>
public sealed record Device(
    IPAddress IpAddress,
    PhysicalAddress? MacAddress,
    string? HostName,
    bool IsThisComputer,
    bool RespondedToPing,
    DateTimeOffset LastSeen)
{
    public string DisplayName =>
        (HostName ?? "—") + (IsThisComputer ? " (this computer)" : "");

    public string IpText => IpAddress.ToString();

    public string MacText => MacAddress is null
        ? "—"
        : string.Join(":", MacAddress.GetAddressBytes().Select(b => b.ToString("X2")));

    /// <summary>Numeric sort key so .9 sorts before .10.</summary>
    public uint SortKey => BinaryPrimitives.ReadUInt32BigEndian(IpAddress.GetAddressBytes());

    public Uri WebUri => new($"http://{IpAddress}/");
}
