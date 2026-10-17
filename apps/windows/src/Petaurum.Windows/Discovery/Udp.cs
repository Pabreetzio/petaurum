using System.Net;
using System.Net.Sockets;

namespace Petaurum.Windows.Discovery;

/// <summary>Shared plumbing for the UDP name lookups: fire queries, then collect replies until cancelled.</summary>
internal static class Udp
{
    // SIO_UDP_CONNRESET
    private const int IgnoreConnectionResetControl = unchecked((int)0x9800000C);

    public static Socket Open(IPAddress localAddress)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        // Without this, an ICMP "port unreachable" from one host makes Windows fail the next receive.
        socket.IOControl(IgnoreConnectionResetControl, [0, 0, 0, 0], null);
        socket.Bind(new IPEndPoint(localAddress, 0));
        return socket;
    }

    public static async Task SendAsync(Socket socket, byte[] datagram, IPEndPoint target, CancellationToken cancellationToken)
    {
        try
        {
            await socket.SendToAsync(datagram, SocketFlags.None, target, cancellationToken);
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException)
        {
            // No route, or the lookup is already over. Either way there's no reply to wait for.
        }
    }

    /// <summary>Hands every datagram to <paramref name="handle"/> until cancelled. Malformed replies are dropped.</summary>
    public static async Task ReceiveAsync(Socket socket, Action<IPAddress, byte[]> handle, CancellationToken cancellationToken)
    {
        var buffer = new byte[9000];
        var anyEndPoint = new IPEndPoint(IPAddress.Any, 0);
        while (!cancellationToken.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, anyEndPoint, cancellationToken);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset or SocketError.MessageSize)
            {
                continue;
            }
            catch (SocketException)
            {
                return;
            }

            try
            {
                handle(((IPEndPoint)result.RemoteEndPoint).Address, buffer[..result.ReceivedBytes]);
            }
            catch (Exception e) when (e is FormatException or ArgumentException or IndexOutOfRangeException)
            {
            }
        }
    }
}
