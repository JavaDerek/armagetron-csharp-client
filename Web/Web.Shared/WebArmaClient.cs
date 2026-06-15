using Armagetron.Lib;
using Armagetron.Net;

namespace Armagetron.Web
{
    /// <summary>
    /// The browser flavour of <see cref="ArmaClient"/>: identical in every way except that its
    /// transport is a <see cref="WebSocketUdpLink"/> (WebSocket → relay → UDP) instead of a raw
    /// UDP socket, since browsers can't open UDP. All of the protocol/registration/session logic
    /// is inherited unchanged — this override is the single line that makes Armagetron run in a web
    /// page. Construct it, hand it to <c>UiArmaClient(client)</c>, and the existing shell/UI drives
    /// it exactly like the desktop client.
    /// </summary>
    public sealed class WebArmaClient : ArmaClient
    {
        private readonly string _relayUrl;

        /// <param name="relayUrl">The WebSocket relay base URL, e.g. <c>ws://localhost:8765/</c>.</param>
        public WebArmaClient(string relayUrl)
        {
            _relayUrl = relayUrl;
        }

        protected override IUdpLink CreateLink(string host, int port) =>
            new WebSocketUdpLink(_relayUrl, host, port);
    }
}
