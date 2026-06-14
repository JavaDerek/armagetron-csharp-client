using System.Collections.Generic;

namespace Armagetron.Game.UI
{
    /// <summary>A transient on-screen notice (round change, you-crashed, future chat/join).</summary>
    public readonly struct Toast
    {
        public readonly string Text;
        public readonly RenderColor Color;
        public readonly long ExpiresAtMs;
        public Toast(string text, RenderColor color, long expiresAtMs)
        {
            Text = text; Color = color; ExpiresAtMs = expiresAtMs;
        }
    }

    /// <summary>
    /// A small, time-expiring notification stack. The shell pushes a toast when a round
    /// changes or the local cycle crashes (and, once the protocol decodes chat/join messages,
    /// for those too); the HUD renders whatever is still <see cref="Active"/>. Pure and
    /// unit-tested — expiry is driven by the host's clock, not a timer.
    /// </summary>
    public sealed class ToastQueue
    {
        public const long DefaultTtlMs = 3_000;

        private readonly List<Toast> _toasts = new List<Toast>();

        public void Push(string text, RenderColor color, long nowMs, long ttlMs = DefaultTtlMs) =>
            _toasts.Add(new Toast(text, color, nowMs + ttlMs));

        /// <summary>Drop expired toasts and return the survivors, oldest first.</summary>
        public IReadOnlyList<Toast> Active(long nowMs)
        {
            _toasts.RemoveAll(t => t.ExpiresAtMs <= nowMs);
            return _toasts;
        }

        public int Count => _toasts.Count;
    }
}
