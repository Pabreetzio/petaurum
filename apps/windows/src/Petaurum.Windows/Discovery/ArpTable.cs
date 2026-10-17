using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Petaurum.Windows.Discovery;

/// <summary>Reads the Windows IPv4 neighbour (ARP) table via iphlpapi's GetIpNetTable.</summary>
public static class ArpTable
{
    public sealed record Entry(IPAddress IpAddress, PhysicalAddress MacAddress);

    private const int NoError = 0;
    private const int ErrorInsufficientBuffer = 122;

    // MIB_IPNETROW: dwIndex(4) dwPhysAddrLen(4) bPhysAddr[8] dwAddr(4) dwType(4)
    private const int RowSize = 24;
    private const int TypeInvalid = 2;

    [DllImport("iphlpapi.dll")]
    private static extern int GetIpNetTable(IntPtr table, ref int size, bool sort);

    public static IReadOnlyList<Entry> Read()
    {
        var size = 0;
        // The table can grow between the size query and the read, so retry a few times.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = GetIpNetTable(IntPtr.Zero, ref size, false);
            if (result != ErrorInsufficientBuffer || size == 0) return [];

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                result = GetIpNetTable(buffer, ref size, true);
                if (result == ErrorInsufficientBuffer) continue;
                if (result != NoError) return [];
                return ParseTable(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        return [];
    }

    private static List<Entry> ParseTable(IntPtr buffer)
    {
        var count = Marshal.ReadInt32(buffer);
        var entries = new List<Entry>(count);
        for (var i = 0; i < count; i++)
        {
            var row = buffer + 4 + i * RowSize;
            var macLength = Marshal.ReadInt32(row, 4);
            var type = Marshal.ReadInt32(row, 20);
            if (type == TypeInvalid || macLength != 6) continue;

            var mac = new byte[6];
            Marshal.Copy(row + 8, mac, 0, 6);
            if (!IsUnicastMac(mac)) continue;

            var ip = new byte[4];
            Marshal.Copy(row + 16, ip, 0, 4);
            entries.Add(new Entry(new IPAddress(ip), new PhysicalAddress(mac)));
        }
        return entries;
    }

    // Rejects all-zero (unresolved), broadcast and multicast MACs (low bit of first octet set).
    private static bool IsUnicastMac(byte[] mac) =>
        mac.Any(b => b != 0) && (mac[0] & 0x01) == 0;
}
