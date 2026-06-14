using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Armagetron.Lib
{
    /// <summary>
    /// A monotonic millisecond clock shared by the session (which stamps remote-cycle sync
    /// times) and the facade (which dead-reckons those cycles forward at snapshot time).
    /// Both must read the SAME clock base or extrapolation would compare timestamps from
    /// two unrelated clocks. netstandard2.1 has no <c>Environment.TickCount64</c>, so we use
    /// <see cref="Stopwatch"/> — the same source <see cref="Armagetron.Net.ArmagetronSessionBase"/>
    /// already ticks on. Trivial wall-clock read; excluded from coverage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static class MonoClock
    {
        public static long NowMs() => Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
    }
}
