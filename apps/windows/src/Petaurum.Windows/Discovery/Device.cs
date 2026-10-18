using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;

namespace Petaurum.Windows.Discovery;

/// <summary>A device seen on the local network. See specs/domain/device.md.</summary>
public sealed record Device(
    IPAddress IpAddress,
    PhysicalAddress? MacAddress,
    string? HostName,
    string? Vendor,
    bool IsThisComputer,
    bool RespondedToPing,
    DateTimeOffset LastSeen)
{
    public string DisplayName =>
        (HostName ?? "Unknown device") + (IsThisComputer ? " (this computer)" : "");

    public string IpText => IpAddress.ToString();

    /// <summary>Null when unknown, so the UI can leave it out.</summary>
    public string? VendorText =>
        Vendor ?? (MacAddress is not null && OuiVendors.IsLocallyAdministered(MacAddress) ? "Private address" : null);

    /// <summary>Null when unknown, so the UI can leave it out.</summary>
    public string? MacText => MacAddress is null
        ? null
        : string.Join(":", MacAddress.GetAddressBytes().Select(b => b.ToString("X2")));

    /// <summary>Numeric sort key so .9 sorts before .10.</summary>
    public uint SortKey => BinaryPrimitives.ReadUInt32BigEndian(IpAddress.GetAddressBytes());

    public Uri WebUri => new($"http://{IpAddress}/");
}
