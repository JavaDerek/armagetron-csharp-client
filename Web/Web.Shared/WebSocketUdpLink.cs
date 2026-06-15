using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Armagetron.Net;

namespace Armagetron.Web
{
    /// <summary>
    /// An <see cref="IUdpLink"/> that carries the game's UDP datagrams over a WebSocket to a relay
    /// (see Web/WsRelay), which forwards them to the real server's UDP port. This is the entire
    /// reason the rest of the C# client — protocol codec, session, ArmaLib facade — runs unchanged
    /// in the browser: only the bottom transport edge differs, swapped in via <c>WebArmaClient</c>.
    ///
    /// Framing: one WebSocket BINARY message == one UDP datagram (the relay enforces the same on
    /// the wire side). The blocking <see cref="IUdpLink"/> contract is bridged onto the async
    /// WebSocket API by a background receive pump that enqueues datagrams; <see cref="Receive"/>
    /// blocks on that queue with a timeout, exactly like the socket link's <c>ReceiveTimeout</c>.
    ///
    /// Uses <see cref="ClientWebSocket"/>, which works on desktop .NET (so the whole path is
    /// verifiable headlessly) AND in Blazor WASM (with threads enabled — see Web/Game.Web).
    /// </summary>
    public sealed class WebSocketUdpLink : IUdpLink
    {
        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private readonly BlockingCollection<byte[]> _incoming = new BlockingCollection<byte[]>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Connect to <paramref name="relayUrl"/> (e.g. <c>ws://localhost:8765/</c>) asking it to
        /// bridge to <paramref name="targetHost"/>:<paramref name="targetPort"/> (the game server).
        /// Connects synchronously so this behaves like the UDP link, which binds in its ctor.
        /// </summary>
        public WebSocketUdpLink(string relayUrl, string targetHost, int targetPort)
        {
            string sep = relayUrl.Contains("?") ? "&" : "?";
            var uri = new Uri($"{relayUrl}{sep}host={Uri.EscapeDataString(targetHost)}&port={targetPort}");
            _ws.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
            _ = Task.Run(ReceivePumpAsync);
        }

        public void Send(byte[] datagram)
        {
            _sendLock.Wait();
            try
            {
                _ws.SendAsync(new ArraySegment<byte>(datagram), WebSocketMessageType.Binary,
                              endOfMessage: true, _cts.Token).GetAwaiter().GetResult();
            }
            catch (Exception) { /* link is torn down; session loop will observe Receive timeouts */ }
            finally { _sendLock.Release(); }
        }

        /// <summary>Next datagram received over the socket, or null on timeout.</summary>
        public byte[]? Receive(int timeoutMillis)
        {
            try
            {
                return _incoming.TryTake(out byte[]? d, timeoutMillis) ? d : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task ReceivePumpAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token)
                                          .ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (ms.Length > 0) _incoming.Add(ms.ToArray());
                }
            }
            catch (Exception)
            {
                // Connection dropped — stop pumping; Receive will simply time out from here on.
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { /* ignore */ }
            try
            {
                if (_ws.State == WebSocketState.Open)
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                       .GetAwaiter().GetResult();
            }
            catch { /* ignore */ }
            _ws.Dispose();
            _cts.Dispose();
            _sendLock.Dispose();
            _incoming.Dispose();
        }
    }
}
