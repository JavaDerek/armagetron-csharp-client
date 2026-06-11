using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Armagetron.Net
{
    /// <summary>
    /// Live UDP transport over <see cref="UdpClient"/> (works on desktop, Android,
    /// iOS via Mono/.NET). Not unit-tested — exercised by the bot against a real
    /// listen server. A front-end may substitute its own <see cref="IUdpLink"/>.
    /// Excluded from coverage: this is the raw socket I/O boundary, verified by the
    /// live-server gate (step 4 of the TDD loop in CLAUDE.md), not by unit tests.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class UdpLink : IUdpLink
    {
        private readonly UdpClient _client;

        public UdpLink(string host, int port)
        {
            _client = new UdpClient();
            _client.Connect(host, port);
        }

        public void Send(byte[] datagram) => _client.Send(datagram, datagram.Length);

        public byte[]? Receive(int timeoutMillis)
        {
            _client.Client.ReceiveTimeout = timeoutMillis;
            try
            {
                IPEndPoint? remote = null;
                return _client.Receive(ref remote);
            }
            catch (SocketException)
            {
                return null; // timeout / transient
            }
        }

        public void Dispose() => _client.Dispose();
    }
}
