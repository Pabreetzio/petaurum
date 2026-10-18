using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Petaurum.Windows.Discovery;

/// <summary>Just enough of the DNS wire format (RFC 1035) to send mDNS queries and read the replies.</summary>
internal static class DnsMessage
{
    public const ushort TypeA = 1;
    public const ushort TypePtr = 12;
    public const ushort TypeTxt = 16;
    public const ushort TypeSrv = 33;
    private const ushort ClassIn = 1;
    private const int MaxPointerJumps = 64;

    public abstract record Record(string[] Name);
    public sealed record A(string[] Name, IPAddress Address) : Record(Name);
    public sealed record Ptr(string[] Name, string[] Target) : Record(Name);
    public sealed record Srv(string[] Name, string[] Target) : Record(Name);
    public sealed record Txt(string[] Name, IReadOnlyDictionary<string, string> Values) : Record(Name);

    /// <summary>A query with one question per name, e.g. ("_http._tcp.local", TypePtr).</summary>
    public static byte[] Query(ushort id, IReadOnlyCollection<(string Name, ushort Type)> questions)
    {
        var bytes = new List<byte>(512);
        WriteUInt16(bytes, id);
        WriteUInt16(bytes, 0); // standard query
        WriteUInt16(bytes, (ushort)questions.Count);
        WriteUInt16(bytes, 0);
        WriteUInt16(bytes, 0);
        WriteUInt16(bytes, 0);
        foreach (var (name, type) in questions)
        {
            foreach (var label in name.Split('.'))
            {
                var encoded = Encoding.UTF8.GetBytes(label);
                bytes.Add((byte)encoded.Length);
                bytes.AddRange(encoded);
            }
            bytes.Add(0);
            WriteUInt16(bytes, type);
            WriteUInt16(bytes, ClassIn);
        }
        return bytes.ToArray();
    }

    /// <summary>
    /// The A, PTR, SRV and TXT records from every section of a reply. A reply that is cut short
    /// still yields the records before the damage.
    /// </summary>
    public static List<Record> ParseRecords(byte[] message)
    {
        var records = new List<Record>();
        if (message.Length < 12 || (message[2] & 0x80) == 0) return records; // not a response

        var questions = ReadUInt16(message, 4);
        var recordCount = ReadUInt16(message, 6) + ReadUInt16(message, 8) + ReadUInt16(message, 10);
        var offset = 12;
        try
        {
            for (var i = 0; i < questions; i++)
            {
                ReadName(message, ref offset);
                offset += 4;
            }
            for (var i = 0; i < recordCount; i++)
            {
                var name = ReadName(message, ref offset);
                var type = ReadUInt16(message, offset);
                var length = ReadUInt16(message, offset + 8);
                var data = offset + 10;
                offset = data + length;
                if (offset > message.Length) break;

                var record = ReadData(message, name, type, data, length);
                if (record is not null) records.Add(record);
            }
        }
        catch (FormatException)
        {
        }
        return records;
    }

    private static Record? ReadData(byte[] message, string[] name, ushort type, int offset, int length)
    {
        switch (type)
        {
            case TypeA when length == 4:
                return new A(name, new IPAddress(message.AsSpan(offset, 4)));
            case TypePtr:
                return new Ptr(name, ReadName(message, ref offset));
            case TypeSrv when length > 6:
                offset += 6; // priority, weight, port
                return new Srv(name, ReadName(message, ref offset));
            case TypeTxt:
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var end = offset + length; offset < end;)
                {
                    var entryLength = message[offset++];
                    if (offset + entryLength > end) break;
                    var entry = Encoding.UTF8.GetString(message, offset, entryLength);
                    offset += entryLength;
                    var equals = entry.IndexOf('=');
                    if (equals > 0) values.TryAdd(entry[..equals], entry[(equals + 1)..]);
                }
                return new Txt(name, values);
            default:
                return null;
        }
    }

    private static string[] ReadName(byte[] message, ref int offset)
    {
        var labels = new List<string>();
        var position = offset;
        var jumps = 0;
        while (true)
        {
            if (position >= message.Length) throw new FormatException("Name runs past the end of the message.");
            var length = message[position];
            if (length == 0)
            {
                position++;
                break;
            }
            if ((length & 0xC0) == 0xC0)
            {
                if (position + 1 >= message.Length || ++jumps > MaxPointerJumps)
                    throw new FormatException("Bad compression pointer.");
                if (jumps == 1) offset = position + 2;
                position = ((length & 0x3F) << 8) | message[position + 1];
                continue;
            }
            if (position + 1 + length > message.Length) throw new FormatException("Label runs past the end of the message.");
            labels.Add(Encoding.UTF8.GetString(message, position + 1, length));
            position += 1 + length;
        }
        if (jumps == 0) offset = position;
        return labels.ToArray();
    }

    private static ushort ReadUInt16(byte[] message, int offset)
    {
        if (offset + 2 > message.Length) throw new FormatException("Message is cut short.");
        return BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset));
    }

    private static void WriteUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }
}
