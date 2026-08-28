namespace FourCorners.Scripts.Components.Command
{
    /// <summary>
    /// Why the server refused a command.
    ///
    /// A channel with no failure signal is half a channel: without this the client cannot tell a
    /// command that was applied from one that was silently dropped, and the only honest thing its
    /// HUD could do is nothing. Every rejection path on the server names its reason here.
    ///
    /// Extending this enum is the sanctioned way to add a rejection — handlers introduced in later
    /// tiers append their own (insufficient funds, level cap) rather than reusing
    /// <see cref="Unknown"/>.
    /// </summary>
    public enum BaseCommandRejection : byte
    {
        /// <summary>Reserved. A reason a client build is too old to have a name for.</summary>
        Unknown = 0,

        /// <summary>The command type was <see cref="BaseCommandType.None"/>.</summary>
        MalformedCommand = 1,

        /// <summary>The match is not in progress, so there is nothing to command.</summary>
        MatchNotActive = 2,

        /// <summary>
        /// The sender holds no corner. Covers a spectator, a connection whose slot was released,
        /// and a client forging commands — all of which are the same fact from here.
        /// </summary>
        NotYourBase = 3,

        /// <summary>The sender's base was destroyed. They watch; they do not build.</summary>
        Eliminated = 4,

        /// <summary>
        /// The sender owns a slot but no live base answers to it. Normal for the handful of frames
        /// between being granted a corner and BaseAllocationSystem activating it.
        /// </summary>
        BaseUnavailable = 5,

        // ── Raised by command handlers rather than the dispatcher ────────────────────────────
        // The dispatcher settles identity; everything below is a question only the system that
        // implements a particular command can answer.

        /// <summary>The command costs more gold than the corner has.</summary>
        InsufficientFunds = 6,

        /// <summary>The building is already at its highest level.</summary>
        LevelCapped = 7,

        /// <summary>
        /// The addressed building does not exist — a slot outside the range this base has, or a
        /// building the command does not apply to. TargetSlot is unvalidated client input until a
        /// handler checks it, and this is that check failing.
        /// </summary>
        NoSuchBuilding = 8
    }
}
