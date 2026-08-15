using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// How long the current match has been running, and whether it has gone into sudden death.
    /// Lives on the MatchStateTag entity alongside <see cref="MatchState"/>.
    ///
    /// Split out of MatchState because it has exactly one writer — MatchClockSystem — while
    /// MatchState is written by the accept, disconnect, start and outcome systems. Bundling a
    /// per-frame counter into a component four other systems read-modify-write is how you get a
    /// stale copy replayed over someone else's phase change, which has already happened once here.
    /// </summary>
    public struct MatchClock : IComponentData
    {
        /// <summary>Seconds since the match went Active.</summary>
        public float ElapsedSeconds;

        /// <summary>
        /// True once the clock has passed the sudden-death threshold and bases have started
        /// taking damage on their own. Latched so the escalation cannot be un-triggered.
        /// </summary>
        public bool SuddenDeathActive;
    }
}
