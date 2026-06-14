using Armagetron.Protocol;

namespace Armagetron.Game.UI
{
    /// <summary>Where a connection attempt stands, as the UI sees it.</summary>
    public enum ConnectionStatus
    {
        /// <summary>Not connected and not trying.</summary>
        Idle,
        /// <summary>A connect attempt is in flight (login + desc=201 registration race).</summary>
        Connecting,
        /// <summary>Registered and in the game; <see cref="IUiClient.Snapshot"/> yields cycles.</summary>
        Connected,
        /// <summary>The attempt failed (server down, rejected, or all retries exhausted).</summary>
        Failed,
    }

    /// <summary>
    /// The narrow surface the UI (the <see cref="AppShell"/> and the front-end host) needs
    /// from a game client, with NO threading or socket detail exposed. ArmaLib's
    /// <c>UiArmaClient</c> adapts the real <c>ArmaClient</c> to this; tests use a fake. Defined
    /// in Core.Protocol (the dependency floor) so the pure screen logic can be unit-tested
    /// without ArmaLib or a socket.
    /// </summary>
    public interface IUiClient
    {
        /// <summary>Current connection state (drives the Connecting → Playing transition).</summary>
        ConnectionStatus Status { get; }

        /// <summary>A human-readable reason for the last failure, or null.</summary>
        string? LastError { get; }

        /// <summary>Start connecting in the background; <see cref="Status"/> reports progress.</summary>
        void BeginConnect(string host, int port, string name);

        /// <summary>Tear down any connection and return to <see cref="ConnectionStatus.Idle"/>.</summary>
        void Disconnect();

        /// <summary>The locally-controlled cycle's id, or -1 before it exists.</summary>
        int MyCycleId { get; }

        /// <summary>Render-ready snapshot of every known cycle (dead-reckoned to now).</summary>
        CycleSnapshot[] Snapshot();

        /// <summary>Queue a left/right turn for the local cycle (no-op if not playing).</summary>
        void TurnLeft();
        void TurnRight();
    }
}
