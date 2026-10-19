using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Petaurum.Windows.Discovery;

/// <summary>An IPv4 subnet this computer is attached to.</summary>
public sealed record LocalSubnet(IPAddress LocalAddress, int PrefixLength, PhysicalAddress? LocalMac, string InterfaceName,
    IPAddress? Gateway)
{
    private const int MaxHosts = 1024;

    private uint Mask => PrefixLength == 0 ? 0 : uint.MaxValue << (32 - PrefixLength);
    private uint Network => ToUInt(LocalAddress) & Mask;

    public bool Contains(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork && (ToUInt(address) & Mask) == Network;

    /// <summary>Every host address in the subnet, excluding network and broadcast.</summary>
    public IEnumerable<IPAddress> Hosts()
    {
        if (PrefixLength >= 31) yield break;
        var count = 1u << (32 - PrefixLength);
        for (var i = 1u; i < count - 1; i++)
            yield return FromUInt(Network + i);
    }

    /// <summary>
    /// Active IPv4 subnets, preferring interfaces with a default gateway so that
    /// virtual adapters (Hyper-V, WSL) are skipped. Large subnets shrink to the local /24.
    /// </summary>
    public static IReadOnlyList<LocalSubnet> Discover()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Nic: n, Props: n.GetIPProperties()))
            .ToList();

        var withGateway = candidates
            .Where(c => c.Props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                          && !g.Address.Equals(IPAddress.Any)))
            .ToList();
        if (withGateway.Count > 0) candidates = withGateway;

        var subnets = new List<LocalSubnet>();
        foreach (var (nic, props) in candidates)
        {
            var mac = nic.GetPhysicalAddress();
            var gateway = props.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;
                // Skip link-local (169.254/16): there's no DHCP lease, so nothing useful to find.
                if (unicast.Address.GetAddressBytes() is [169, 254, ..]) continue;

                var prefix = unicast.PrefixLength;
                if ((1L << (32 - prefix)) - 2 > MaxHosts) prefix = 24;

                subnets.Add(new LocalSubnet(unicast.Address, prefix,
                    mac.GetAddressBytes().Length == 6 ? mac : null, nic.Name, gateway));
            }
        }
        return subnets;
    }

    private static uint ToUInt(IPAddress address) =>
        BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());

    private static IPAddress FromUInt(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes);
    }
}
