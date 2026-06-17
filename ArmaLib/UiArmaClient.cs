using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Armagetron.Game;
using Armagetron.Game.UI;

namespace Armagetron.Lib
{
    /// <summary>
    /// Adapts the real <see cref="ArmaClient"/> to the UI-facing <see cref="IUiClient"/> the
    /// <c>AppShell</c> drives. It turns the blocking, threaded <see cref="ArmaClient.Connect"/>
    /// into a non-blocking <see cref="BeginConnect"/> + observable <see cref="Status"/>, and
    /// buffers round-lifecycle events fired on the protocol thread so the UI thread can drain
    /// them in <c>AppShell.Tick</c>. Threading/socket lifecycle here is verified by the
    /// live-server gate (CLAUDE.md step 4), like ArmaClient itself, so it is excluded from
    /// coverage; the pure consumer (AppShell) is unit-tested against a fake.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class UiArmaClient : IUiClient, IDisposable
    {
        private readonly ArmaClient _client;
        private readonly object _gate = new object();
        private readonly Queue<MatchEvent> _events = new Queue<MatchEvent>();
        private volatile int _status = (int)ConnectionStatus.Idle;
        private Thread? _connectThread;

        /// <summary>Default: a normal UDP-socket client (desktop / Android / iOS).</summary>
        public UiArmaClient() : this(new ArmaClient()) { }

        /// <summary>
        /// Inject a specific <see cref="ArmaClient"/> — used by the web head to supply a
        /// <c>WebArmaClient</c> whose transport is WebSocket→relay→UDP, since browsers cannot open
        /// raw UDP sockets. The shell/UI is unaware of the difference.
        /// </summary>
        public UiArmaClient(ArmaClient client)
        {
            _client = client;
            _client.RoundStarted += (_, _) => Enqueue(MatchEvent.RoundStart);
            _client.RoundEnded   += (_, _) => Enqueue(MatchEvent.RoundEnd);
            _client.Spawned      += (_, e) => { if (e.IsMine) Enqueue(MatchEvent.LocalSpawned); };
            _client.Died         += (_, e) => { if (e.IsMine) Enqueue(MatchEvent.LocalDied); };
        }

        public ConnectionStatus Status => (ConnectionStatus)_status;
        public string? LastError { get; private set; }
        public int MyCycleId => _client.MyCycleId;
        public CycleSnapshot[] Snapshot() => _client.Snapshot();
        public void TurnLeft() => _client.TurnLeft();
        public void TurnRight() => _client.TurnRight();

        public void BeginConnect(string host, int port, string name)
        {
            if (Status == ConnectionStatus.Connecting) return;
            LastError = null;
            _status = (int)ConnectionStatus.Connecting;
            _connectThread = new Thread(() =>
            {
                try
                {
                    bool ok = _client.Connect(host, port, name);
                    if (ok) { _status = (int)ConnectionStatus.Connected; }
                    else { LastError = "COULD NOT REGISTER"; _status = (int)ConnectionStatus.Failed; }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message.ToUpperInvariant();
                    _status = (int)ConnectionStatus.Failed;
                }
            })
            { IsBackground = true, Name = "UiArmaClient.Connect" };
            _connectThread.Start();
        }

        public void Disconnect()
        {
            _client.Disconnect();
            _status = (int)ConnectionStatus.Idle;
        }

        public IReadOnlyList<MatchEvent> DrainEvents()
        {
            lock (_gate)
            {
                if (_events.Count == 0) return Array.Empty<MatchEvent>();
                var list = new List<MatchEvent>(_events);
                _events.Clear();
                return list;
            }
        }

        private void Enqueue(MatchEvent ev)
        {
            lock (_gate) _events.Enqueue(ev);
        }

        public void Dispose() => _client.Dispose();
    }
}
