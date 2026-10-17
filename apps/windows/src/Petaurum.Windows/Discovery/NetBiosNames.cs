using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Petaurum.Windows.Discovery;

/// <summary>
/// NetBIOS node status lookups (RFC 1002, NBSTAT) over UDP 137. Windows and Samba machines and most
/// NAS boxes answer with their name table. See specs/features/device-list.md, "Resolve names".
/// </summary>
public static class NetBiosNames
{
    private const int Port = 137;
    private static readonly TimeSpan ResendDelay = TimeSpan.FromMilliseconds(500);

    // Header (id, flags 0, one question) then the name "*" padded with NULs, first-level encoded
    // (each nibble + 'A', so '*' is "CK" and NUL is "AA"), type NBSTAT (0x21), class IN.
    private static readonly byte[] NodeStatusRequest =
    [
        0x50, 0x54, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x20, (byte)'C', (byte)'K', ..Enumerable.Repeat((byte)'A', 30), 0x00,
        0x00, 0x21, 0x00, 0x01,
    ];

    private const byte WorkstationSuffix = 0x00;
    private const ushort GroupNameFlag = 0x8000;

    /// <summary>Asks every address at once and returns the names that came back within <paramref name="timeout"/>.</summary>
    public static async Task<Dictionary<IPAddress, string>> QueryAsync(
        IReadOnlyCollection<IPAddress> addresses, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var names = new Dictionary<IPAddress, string>();
        using var socket = Udp.Open(IPAddress.Any);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        var receiving = Udp.ReceiveAsync(socket, (from, reply) =>
        {
            if (ParseWorkstationName(reply) is { } name)
                lock (names) names.TryAdd(from, name);
        }, deadline.Token);

        try
        {
            // Twice, because UDP can drop either the query or the reply.
            for (var round = 0; round < 2; round++)
            {
                foreach (var address in addresses)
                {
                    lock (names) if (names.ContainsKey(address)) continue;
                    await Udp.SendAsync(socket, NodeStatusRequest, new IPEndPoint(address, Port), deadline.Token);
                }
                await Task.Delay(ResendDelay, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        await receiving;
        cancellationToken.ThrowIfCancellationRequested();
        lock (names) return new Dictionary<IPAddress, string>(names);
    }

    /// <summary>The first unique name with suffix &lt;00&gt; (the machine name) in a node status reply.</summary>
    private static string? ParseWorkstationName(byte[] reply)
    {
        if (reply.Length < 12 || (reply[2] & 0x80) == 0) return null; // not a response
        if (BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(6)) == 0) return null; // no answers

        var offset = SkipName(reply, 12);
        offset += 2 + 2 + 4; // type, class, TTL
        if (offset + 3 > reply.Length) return null;
        offset += 2; // data length
        var count = reply[offset++];

        for (var i = 0; i < count && offset + 18 <= reply.Length; i++, offset += 18)
        {
            var suffix = reply[offset + 15];
            var flags = BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(offset + 16));
            if (suffix != WorkstationSuffix || (flags & GroupNameFlag) != 0) continue;

            var name = Encoding.Latin1.GetString(reply, offset, 15).TrimEnd(' ', '\0');
            if (name.Length > 0) return name;
        }
        return null;
    }

    private static int SkipName(byte[] message, int offset)
    {
        while (offset < message.Length)
        {
            var length = message[offset];
            if (length == 0) return offset + 1;
            if ((length & 0xC0) == 0xC0) return offset + 2; // compression pointer
            offset += 1 + length;
        }
        throw new FormatException("Name runs past the end of the reply.");
    }
}
