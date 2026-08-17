using Unity.Entities;

namespace FourCorners.Scripts.Components.Command
{
    /// <summary>
    /// A command the server has already vetted, addressed to the base it may act on.
    ///
    /// This is the boundary between untrusted and trusted. A <c>BaseCommandRequest</c> is whatever
    /// a client chose to send; a <see cref="BaseCommand"/> exists only because
    /// ServerBaseCommandSystem confirmed the sender occupies a live corner and derived
    /// <see cref="BaseEntity"/> from that occupancy. Handlers may therefore act on
    /// <see cref="BaseEntity"/> without re-checking who asked — that question is settled — and
    /// must never take a base entity from a client instead.
    ///
    /// One entity per accepted command rather than a buffer on the base: several commands can land
    /// on one base in one frame, and a buffer would leave every handler arguing over who clears it.
    ///
    /// Lifetime is exactly one frame. The intent is created through the end-of-frame ECB, so it
    /// first exists on the frame after the RPC arrived, and BaseCommandCleanupSystem destroys every
    /// one it sees. A handler gets one look at it.
    /// </summary>
    public struct BaseCommand : IComponentData
    {
        /// <summary>The sender's own base. Derived by the server, never supplied by the client.</summary>
        public Entity BaseEntity;

        /// <summary>Which corner sent this, for logging and for addressing team-wide effects.</summary>
        public Team.TeamNumber Team;

        /// <summary>
        /// The connection that asked, so a handler can answer with its own rejection.
        ///
        /// Stale by construction, exactly like <c>Engagement.Target</c>: a frame passes between the
        /// RPC arriving and this being readable, and the player may have dropped in between. Any
        /// handler that sends to it must confirm it still exists first — recording an RPC aimed at
        /// a destroyed connection throws during playback, and one throw discards every command
        /// buffered by every system that frame.
        /// </summary>
        public Entity SourceConnection;

        public BaseCommandType Type;

        /// <summary>
        /// Which of the addressed building's slots this refers to; meaningless for commands that
        /// address the base itself.
        ///
        /// Deliberately not range-checked by the dispatcher, which has no idea what a slot means
        /// for any given command type. The handler that knows the count is the one that must
        /// bounds-check it, and it is reading a number a client chose — treat it accordingly.
        /// </summary>
        public byte TargetSlot;

        /// <summary>
        /// Set by whichever handler acted on this.
        ///
        /// Its only job is diagnostics: a command type nobody handles is otherwise a perfectly
        /// silent no-op, which is the most expensive kind of bug to find. Written directly rather
        /// than through the ECB — handlers own the entity for the frame, and a deferred write would
        /// land after the cleanup pass had already judged it.
        /// </summary>
        public bool Handled;
    }
}
