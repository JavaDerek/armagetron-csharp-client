using System;

namespace Armagetron.Net
{
    /// <summary>
    /// Abstraction over the UDP transport so session logic is testable without a
    /// real socket, and so a front-end (e.g. Unity) can substitute its own link.
    /// </summary>
    public interface IUdpLink : IDisposable
    {
        void Send(byte[] datagram);

        /// <summary>Next datagram received, or null on timeout.</summary>
        byte[]? Receive(int timeoutMillis);
    }
}
