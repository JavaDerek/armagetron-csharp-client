using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Armagetron.Game;
using Armagetron.Net;

namespace Armagetron.Lib
{
    /// <summary>
    /// The ArmaLib facade: a thin, beginner-friendly SDK over Core.Protocol + Core.Net.
    /// A front-end (desktop, Android, universal, Oculus) talks to an Armagetron 0.2.9.x
    /// server entirely through this surface and never touches a protocol primitive —
    /// descriptor IDs, REAL encoding, netobject id reservation and the desc=311 priming
    /// sequence all stay hidden below it.
    ///
    /// Usage:
    /// <code>
    ///   var client = new ArmaClient();
    ///   client.Spawned += (_, e) => ...;
    ///   if (client.Connect("192.168.68.61", 4534, "Player1"))
    ///   {
    ///       client.TurnLeft();
    ///       var cycles = client.Snapshot(Environment.TickCount64); // render these
    ///   }
    ///   client.Disconnect();
    /// </code>
    ///
    /// Excluded from coverage: connection, threading and socket lifecycle are verified by
    /// the live-server gate (CLAUDE.md step 4), exactly like <see cref="ArmagetronSessionBase"/>
    /// and <c>UdpLink</c>. The pure event logic underneath is unit-tested via
    /// <see cref="GameEventTracker"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ArmaClient : IDisposable
    {
        private readonly GameWorld _world = new GameWorld();
        private readonly GameEventTracker _events = new GameEventTracker();

        private ArmaSession? _session;
        private Thread? _loopThread;

        public ArmaClient()
        {
            // Re-raise the tracker's high-level events as our own public events so the GUI
            // subscribes to one object. These fire on the protocol thread (see Connect).
            _events.RoundStarted  += (_, e) => RoundStarted?.Invoke(this, e);
            _events.RoundEnded    += (_, e) => RoundEnded?.Invoke(this, e);
            _events.CyclesChanged += (_, e) => CyclesChanged?.Invoke(this, e);
            _events.Spawned       += (_, e) => Spawned?.Invoke(this, e);
            _events.Died          += (_, e) => Died?.Invoke(this, e);
        }

        // ── Events (handlers run on the protocol thread) ────────────────────────

        /// <summary>A new round has begun.</summary>
        public event EventHandler? RoundStarted;
        /// <summary>The current round has ended.</summary>
        public event EventHandler? RoundEnded;
        /// <summary>Any cycle's position changed — re-snapshot and redraw.</summary>
        public event EventHandler? CyclesChanged;
        /// <summary>A cycle appeared (first sighting this round). <see cref="CycleEventArgs.IsMine"/>
        /// flags our own cycle.</summary>
        public event EventHandler<CycleEventArgs>? Spawned;
        /// <summary>A cycle died (crashed). Fires exactly once per death.</summary>
        public event EventHandler<CycleEventArgs>? Died;

        // ── Connection ──────────────────────────────────────────────────────────

        /// <summary>
        /// Connect to <paramref name="host"/>:<paramref name="port"/> as
        /// <paramref name="name"/>, complete the login + registration handshake, and start
        /// the background session loop. Returns true once our cycle exists and the game is
        /// joinable; false if registration could not be completed.
        ///
        /// Registration (desc=201) is a one-shot, timing-sensitive race against the server.
        /// It is driven here on the CALLER's (uncontended) thread before the loop thread
        /// starts — a render-starved thread loses the race and the server replies "cheating".
        /// On timeout/rejection we retry on a FRESH socket, which also escapes the server's
        /// post-rejection mute. Once registered, the session is handed to a background thread
        /// for the gameplay phase.
        /// </summary>
        public bool Connect(string host, int port, string name,
                            int timeoutMs = 45_000, int maxAttempts = 10)
        {
            if (_session != null)
                throw new InvalidOperationException("Already connected; call Disconnect first.");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var candidate = new ArmaSession(CreateLink(host, port), name, _world, _events);
                if (candidate.RunUntilPlaying(timeoutMs))
                {
                    _session = candidate;
                    StartLoop();
                    return true;
                }
                candidate.Dispose(); // closes the socket; next attempt gets a new slot
            }
            return false;
        }

        /// <summary>Queue a left turn for the local cycle (no-op if not connected).</summary>
        public void TurnLeft() => _session?.QueueTurn(TurnDir.Left);

        /// <summary>Queue a right turn for the local cycle (no-op if not connected).</summary>
        public void TurnRight() => _session?.QueueTurn(TurnDir.Right);

        /// <summary>Stop the session loop and release the socket. Safe to call repeatedly.</summary>
        public void Disconnect()
        {
            var session = _session;
            if (session == null) return;

            session.RequestStop();
            _loopThread?.Join(millisecondsTimeout: 1000);
            session.Dispose();
            _session    = null;
            _loopThread = null;
        }

        // ── Render data ───────────────────────────────────────────────────────

        /// <summary>The locally-controlled cycle's id, or -1 before it is created.</summary>
        public int MyCycleId => _world.MyCycleId;

        /// <summary>
        /// A render-ready snapshot of every known cycle (head position, heading, trail).
        /// Remote cycles are dead-reckoned forward to "now" so they move smoothly between
        /// sparse server syncs — the facade owns the clock (shared with the session's sync
        /// timestamps), so the caller just renders whatever this returns.
        /// </summary>
        public CycleSnapshot[] Snapshot() => _world.Snapshot(MonoClock.NowMs());

        // ── Seams / lifecycle ───────────────────────────────────────────────────

        /// <summary>
        /// Create the UDP transport for a connection attempt. The default opens a real
        /// <see cref="UdpLink"/>; overridable so tests can inject a fake link without sockets.
        /// </summary>
        protected virtual IUdpLink CreateLink(string host, int port) => new UdpLink(host, port);

        private void StartLoop()
        {
            _loopThread = new Thread(() => _session!.RunLoop())
            {
                IsBackground = true,
                Name         = "ArmaLib.ProtocolThread",
            };
            _loopThread.Start();
        }

        public void Dispose() => Disconnect();
    }
}
