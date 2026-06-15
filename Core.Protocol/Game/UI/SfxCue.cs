using System.Collections.Generic;

namespace Armagetron.Game.UI
{
    /// <summary>
    /// The fixed set of sound effects from the audio manifest. One-shots are queued via
    /// <see cref="SfxCueQueue"/> and played once when the host drains them; the looping cues
    /// (<see cref="EngineLoop"/>, <see cref="WallGrind"/>) are state-driven instead — the host
    /// reads a per-frame boolean (e.g. <see cref="AppShell.EngineRunning"/>) and starts/stops
    /// the loop. Ids map 1:1 to <c>media/audio/sfx/&lt;id&gt;.wav</c>.
    /// </summary>
    public enum SfxId
    {
        UiTap,
        Turn,
        CountdownBeep,
        Go,
        Explosion,
        Win,
        Lose,
        ConnectOk,
        ConnectFail,
        EngineLoop,
        WallGrind,
    }

    /// <summary>
    /// A drain-once queue of one-shot SFX cues. The pure shell pushes a cue the frame its
    /// trigger fires (a turn, a crash, a connect result); the MonoGame host calls
    /// <see cref="Drain"/> once per frame and plays each returned cue a single time. Mirrors
    /// the <see cref="ToastQueue"/> push/observe split, but consume-on-read so a sound never
    /// repeats on later frames. No platform/audio dependency, so it is unit-tested.
    /// </summary>
    public sealed class SfxCueQueue
    {
        private readonly List<SfxId> _pending = new List<SfxId>();

        /// <summary>Queue a cue to be played on the next host drain.</summary>
        public void Push(SfxId id) => _pending.Add(id);

        /// <summary>How many cues are waiting to be drained.</summary>
        public int Count => _pending.Count;

        /// <summary>Return every queued cue (FIFO) and clear the queue — each plays exactly once.</summary>
        public IReadOnlyList<SfxId> Drain()
        {
            if (_pending.Count == 0) return System.Array.Empty<SfxId>();
            var cues = _pending.ToArray();
            _pending.Clear();
            return cues;
        }
    }
}
